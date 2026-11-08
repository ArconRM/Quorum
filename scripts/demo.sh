#!/usr/bin/env bash
# Сквозной сценарий через API Gateway:
#   1) камера + ансамбль из трёх open-vocab детекторов + 40 кадров;
#   2) цикл в гибридном режиме: спорные кадры проверяет разметчик → обучение → оценка → публикация v1;
#   3) ещё 40 кадров, автоматический режим → v2 сравнивается с v1 на новых кадрах;
#   4) кадров нет — цикл пропускается;
#   5) (DEMO_FAILURE=1, локально) воркер остановлен → цикл проваливается, кадры возвращаются камере.
# Требуется: curl, jq. Запуск: ./scripts/demo.sh [http://localhost:8000]
set -euo pipefail
GW=${1:-http://localhost:8000}
RND=$RANDOM
TMP=$(mktemp -d); trap 'rm -rf "$TMP"' EXIT

post()  { curl -sf -X POST  "$GW$1" -H "Content-Type: application/json" ${3:+-H "Authorization: Bearer $3"} -d "$2"; }
patch() { curl -sf -X PATCH "$GW$1" -H "Content-Type: application/json" ${3:+-H "Authorization: Bearer $3"} -d "$2"; }
get()   { curl -sf "$GW$1" ${2:+-H "Authorization: Bearer $2"}; }
login() { post /identity/auth/login "{\"email\":\"$1\",\"password\":\"$2\"}" | jq -r .token; }

upload_frames() { # camera, count, token
  for i in $(seq 1 "$2"); do
    head -c 4096 /dev/urandom > "$TMP/f.jpg"
    curl -sf -X POST "$GW/datasets/cameras/$1/frames" -H "Authorization: Bearer $3" \
      -F "file=@$TMP/f.jpg;type=image/jpeg" >/dev/null
  done
}

# Ждём конца цикла; пока идёт разметка — разметчик разбирает спорные кадры (всё подтверждает).
run_cycle() { # cycleId
  local status last="" reviewed=0
  for _ in $(seq 1 300); do
    status=$(get "/training/cycles/$1" "$ADMIN" | jq -r .status)
    [[ "$status" != "$last" ]] && { echo "   … $status"; last=$status; }
    case "$status" in
      Completed|Failed|Skipped) break ;;
      Labeling)
        code=$(curl -s -o "$TMP/review.json" -w '%{http_code}' -X POST "$GW/labeling/reviews/next" -H "Authorization: Bearer $ANNOTATOR")
        if [[ "$code" == 200 ]]; then
          post "/labeling/reviews/$(jq -r .taskId "$TMP/review.json")" '{"verdict":"Approve"}' "$ANNOTATOR" >/dev/null
          reviewed=$((reviewed + 1)); continue
        fi ;;
    esac
    sleep 1
  done
  [[ "$status" =~ ^(Completed|Failed|Skipped)$ ]] || echo "   ! цикл не завершился за 300 с (статус: $status) — смотри: docker compose logs training"
  [[ $reviewed -gt 0 ]] && echo "   разметчик проверил кадров: $reviewed"
  get "/training/cycles/$1" "$ADMIN" | jq -c '{status, outcome, trainFrames, holdoutFrames, acceptedLabels, failureReason, evaluation}'
}

echo "== 0. Вход: администратор; разметчик регистрируется сам"
ADMIN=$(login admin@quorum.local 'Admin123!')
post /identity/auth/register "{\"email\":\"annotator$RND@quorum.local\",\"password\":\"Passw0rd!\",\"fullName\":\"Разметчик\"}" >/dev/null
ANNOTATOR=$(login "annotator$RND@quorum.local" 'Passw0rd!')

echo "== 1. Камера, учителя, 40 кадров"
CAM=$(post /datasets/cameras '{"name":"Въезд, камера '"$RND"'","task":"Detection","prompt":"license plate","minFramesPerCycle":30,"cycleIntervalHours":24}' "$ADMIN" | jq -r .id)
for t in "grounding-dino-swin-b:1.0" "yolo-world-l:1.0" "owlv2-large:0.8"; do
  post /labeling/teachers "{\"name\":\"${t%%:*}-$RND\",\"kind\":\"OpenVocabDetector\",\"task\":\"Detection\",\"weight\":${t##*:}}" "$ADMIN" >/dev/null || true
done
upload_frames "$CAM" 40 "$ADMIN"
get "/datasets/cameras/$CAM" "$ADMIN" | jq -c '{name, task, newFrames}'

echo "== 2. Цикл 1: Hybrid, порог 0.7 → разметка → проверка спорных → обучение → оценка"
C1=$(post /training/cycles "{\"cameraId\":\"$CAM\",\"validationMode\":\"Hybrid\",\"agreementThreshold\":0.7}" "$ADMIN" | jq -r .id)
run_cycle "$C1"
get "/labeling/runs?cycleId=$C1" "$ADMIN" | jq -c '{status, totalFrames, accepted, rejected, reviewed, teachers: [.teachers[].name]}'
echo "   опубликованная модель:"; get "/training/cameras/$CAM/model" "$ADMIN" | jq -c '{number, architecture, metricName, holdoutMetric, status}'

echo "== 3. Ещё 40 кадров. Цикл 2: Auto — без человека; кандидат против v1 на новых кадрах"
upload_frames "$CAM" 40 "$ADMIN"
C2=$(post /training/cycles "{\"cameraId\":\"$CAM\",\"validationMode\":\"Auto\",\"agreementThreshold\":0.7}" "$ADMIN" | jq -r .id)
run_cycle "$C2"
get "/training/cameras/$CAM/models" "$ADMIN" | jq -c '.[] | {number, status, holdoutMetric}'

echo "== 4. Новых кадров нет → цикл пропускается"
C3=$(post /training/cycles "{\"cameraId\":\"$CAM\"}" "$ADMIN" | jq -r .id)
run_cycle "$C3"

if [[ "${DEMO_FAILURE:-0}" == 1 ]]; then
  echo "== 5. Компенсация: воркер остановлен → разметка падает → снимок отброшен, кадры снова «новые»"
  upload_frames "$CAM" 35 "$ADMIN"
  docker compose stop worker >/dev/null
  C4=$(post /training/cycles "{\"cameraId\":\"$CAM\",\"validationMode\":\"Auto\"}" "$ADMIN" | jq -r .id)
  run_cycle "$C4"
  sleep 3
  get "/datasets/cameras/$CAM" "$ADMIN" | jq -c '{newFrames}'
  docker compose start worker >/dev/null
fi