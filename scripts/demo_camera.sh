#!/usr/bin/env bash
# Сквозной прогон настоящей системы на кадрах камеры курсовой: несколько циклов адаптации подряд.
#
#   ./scripts/demo_camera.sh [лимит кадров на цикл=0 (все)] [gateway=http://localhost:8000]
#
# Перед запуском поднять стек с настоящим воркером и настройками учителей из экспериментов:
#   GDINO_THRESHOLD=0.25 OWLV2_THRESHOLD=0.2 GDINO_MAX_BOX_AREA=0.03 OWLV2_MAX_BOX_AREA=0.03 TRAINING_EPOCHS=30 \
#     docker compose -f docker-compose.yml -f docker-compose.gpu.yml up -d --build
#
# Сценарий (циклы — CYCLES ниже, дни — папки datasets/camera/<день>/images):
#   1) камера «license plate» и три учителя: OWLv2 large, LLMDet large, MM-Grounding DINO large;
#   2) для каждого цикла: залить кадры его дней → запустить цикл → дождаться конца, фиксируя время каждого
#      статуса саги (Snapshotting → Labeling → Training → Evaluating → Completed/Failed/Skipped);
#   3) в Hybrid спорные кадры «разметчик» подтверждает автоматически (как в demo.sh) — это проверка работы
#      системы, а не качества: качество стратегий меряет experiments.student.
# Отчёт — runs/system_demo/<время>/: report.md (статусы и длительности), JSON циклов, прогонов разметки и моделей.
# Сначала стоит прогнать с лимитом (например, 100), чтобы поймать стыковочные ошибки быстро.
# Требуется: curl, jq.
set -euo pipefail
LIMIT=${1:-0}
GW=${2:-http://localhost:8000}
DATA=${DATA:-datasets/camera}
OUT=runs/system_demo/$(date +%Y%m%d-%H%M%S)
TIMEOUT_MIN=${TIMEOUT_MIN:-240}
RND=$RANDOM
mkdir -p "$OUT"

# Цикл: «дни через запятую|режим|порог согласия»
CYCLES=(
  "2025-11-09,2025-11-15|Hybrid|0.85"
  "2025-11-16|Auto|0.85"
  "2025-11-29,2025-11-30|Auto|0.85"
)

log()   { echo "$(date '+%F %T') $*" | tee -a "$OUT/report.md"; }
post()  { curl -sf -X POST "$GW$1" -H "Content-Type: application/json" ${3:+-H "Authorization: Bearer $3"} -d "$2"; }
patch() { curl -sf -X PATCH "$GW$1" -H "Content-Type: application/json" ${3:+-H "Authorization: Bearer $3"} -d "$2"; }
get()   { curl -sf "$GW$1" ${2:+-H "Authorization: Bearer $2"}; }
login() { post /identity/auth/login "{\"email\":\"$1\",\"password\":\"$2\"}" | jq -r .token; }

upload_day() { # camera, day → число залитых кадров
  local n=0 f
  while IFS= read -r -d '' f; do
    curl -sf -X POST "$GW/datasets/cameras/$1/frames" -H "Authorization: Bearer $ADMIN" \
      -F "file=@$f;type=image/jpeg" >/dev/null || { echo "! не загрузился: $f" >&2; continue; }
    n=$((n + 1))
    (( LIMIT > 0 && n >= LIMIT )) && break
  done < <(find "$DATA/$2/images" -maxdepth 1 -name 'frame_*.jpg' -print0 | sort -z)
  echo "$n"
}

wait_cycle() { # cycleId, номер
  local status last="" started=$SECONDS since=$SECONDS reviewed=0 code
  while (( SECONDS - started < TIMEOUT_MIN * 60 )); do
    status=$(get "/training/cycles/$1" "$ADMIN" | jq -r .status) || status=unknown
    if [[ "$status" != "$last" ]]; then
      [[ -n "$last" ]] && log "   $last: $(( SECONDS - since )) с"
      log "   → $status"; last=$status; since=$SECONDS
    fi
    case "$status" in
      Completed|Failed|Skipped) break ;;
      Labeling)
        code=$(curl -s -o "$OUT/.review.json" -w '%{http_code}' -X POST "$GW/labeling/reviews/next" -H "Authorization: Bearer $ANNOTATOR")
        if [[ "$code" == 200 ]]; then
          post "/labeling/reviews/$(jq -r .taskId "$OUT/.review.json")" '{"verdict":"Approve"}' "$ANNOTATOR" >/dev/null || true
          reviewed=$((reviewed + 1)); continue
        fi ;;
    esac
    sleep 5
  done
  log "   итого цикл $2: $(( SECONDS - started )) с, разметчик проверил кадров: $reviewed"
  get "/training/cycles/$1" "$ADMIN" > "$OUT/cycle_$2.json"
  get "/labeling/runs?cycleId=$1" "$ADMIN" > "$OUT/labeling_$2.json" || true
  log "   $(jq -c '{status, outcome, trainFrames, holdoutFrames, acceptedLabels, failureReason, evaluation}' "$OUT/cycle_$2.json")"
  [[ -s "$OUT/labeling_$2.json" ]] && log "   разметка: $(jq -c '{status, totalFrames, accepted, rejected, reviewed}' "$OUT/labeling_$2.json")"
  return 0
}

echo "# Сквозной прогон Quorum на кадрах камеры" > "$OUT/report.md"
log "Gateway $GW, кадры $DATA, лимит на день: ${LIMIT:-0} (0 — все)"

log "== Вход: администратор и разметчик"
# Сразу после «docker compose up» Identity ещё применяет миграции — ждём до двух минут.
ADMIN=""
for _ in $(seq 1 24); do
  ADMIN=$(login admin@quorum.local 'Admin123!' 2>/dev/null) && [[ -n "$ADMIN" && "$ADMIN" != null ]] && break
  sleep 5
done
[[ -n "$ADMIN" && "$ADMIN" != null ]] || { log "! не удалось войти в $GW — проверь: docker compose logs identity gateway"; exit 1; }
post /identity/auth/register "{\"email\":\"annotator$RND@quorum.local\",\"password\":\"Passw0rd!\",\"fullName\":\"Разметчик\"}" >/dev/null
ANNOTATOR=$(login "annotator$RND@quorum.local" 'Passw0rd!')

log "== Камера и учителя"
# Реестр учителей общий для всех камер: разметка задачи берёт всех активных учителей Detection.
# Учителя прошлых запусков (в том числе demo.sh с YOLO-World) иначе попадут в ансамбль и сломают согласие.
for id in $(get "/labeling/teachers?task=Detection" "$ADMIN" | jq -r '.[] | select(.isActive) | .id'); do
  patch "/labeling/teachers/$id" '{"isActive":false}' "$ADMIN" >/dev/null && log "   выключен прежний учитель $id" || true
done
CAM=$(post /datasets/cameras '{"name":"Камера курсовой '"$RND"'","task":"Detection","prompt":"license plate","minFramesPerCycle":30,"cycleIntervalHours":720}' "$ADMIN" | jq -r .id)
log "   камера $CAM"
for t in owlv2-large llmdet-large mm-grounding-dino-large; do
  post /labeling/teachers "{\"name\":\"$t-$RND\",\"kind\":\"OpenVocabDetector\",\"task\":\"Detection\",\"weight\":1.0}" "$ADMIN" >/dev/null \
    && log "   учитель $t-$RND" || log "   ! учитель $t не зарегистрирован"
done

i=0
for spec in "${CYCLES[@]}"; do
  i=$((i + 1))
  IFS='|' read -r days mode thr <<< "$spec"
  log "== Цикл $i: дни $days, режим $mode, порог $thr"
  t0=$SECONDS total=0
  for d in ${days//,/ }; do
    n=$(upload_day "$CAM" "$d"); total=$((total + n))
    log "   залито $d: $n кадров"
  done
  log "   заливка: $total кадров за $(( SECONDS - t0 )) с"
  C=$(post /training/cycles "{\"cameraId\":\"$CAM\",\"validationMode\":\"$mode\",\"agreementThreshold\":$thr}" "$ADMIN" | jq -r .id)
  log "   цикл $C"
  wait_cycle "$C" "$i"
done

log "== Версии модели камеры"
get "/training/cameras/$CAM/models" "$ADMIN" | tee "$OUT/models.json" | jq -c '.[] | {number, status, holdoutMetric}' | tee -a "$OUT/report.md"
rm -f "$OUT/.review.json"
log "Отчёт: $OUT"
