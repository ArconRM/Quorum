#!/usr/bin/env bash
# Скачивает и проверяет все VLM-учителя из llama-swap.yaml: каждой модели отправляется картинка.
# Первый запуск долгий (скачивание по 3–6 ГБ на модель), повторные — секунды.
#
#   ./scripts/warmup_vlm.sh            # все модели из llama-swap.yaml
#   ./scripts/warmup_vlm.sh gemma-3-4b # только эти
#
# В конце — список моделей, которые можно передавать в --teachers.
set -uo pipefail
cd "$(dirname "$0")/.."
URL=${VLM_URL:-http://localhost:8080/v1}
COMPOSE=(docker compose -f docker-compose.yml -f docker-compose.gpu.yml)

if [[ $# -gt 0 ]]; then
  MODELS=("$@")
else
  # Ключи второго уровня в секции models: — это имена моделей.
  mapfile -t MODELS < <(awk '/^models:/{m=1;next} m && /^  [A-Za-z0-9._-]+:/{sub(/:.*/,"");gsub(/ /,"");print}' llama-swap.yaml)
fi

"${COMPOSE[@]}" up -d llm >/dev/null
echo "Жду llama-swap на $URL ..."
for _ in $(seq 1 60); do curl -sf "$URL/models" >/dev/null && break; sleep 2; done

# Картинка 32×32 с белой полосой — содержимое не важно, проверяется, что модель вообще принимает изображения.
IMG=$(python3 - <<'PY'
import base64, struct, zlib
w = h = 32
rows = b"".join(b"\x00" + (b"\xff\xff\xff" if 12 <= y < 20 else b"\x30\x30\x30") * w for y in range(h))
chunk = lambda t, d: struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b"")
print(base64.b64encode(png).decode())
PY
)

ok=() bad=()
for m in "${MODELS[@]}"; do
  echo "== $m: загрузка (первый раз — скачивание, может занять 10+ минут)"
  started=$(date +%s)
  body=$(curl -s --max-time 1900 "$URL/chat/completions" -H "Content-Type: application/json" -d @- <<JSON
{"model":"$m","temperature":0,"max_tokens":16,"messages":[{"role":"user","content":[
 {"type":"image_url","image_url":{"url":"data:image/png;base64,$IMG"}},
 {"type":"text","text":"What is in this image? Answer in three words."}]}]}
JSON
)
  answer=$(python3 -c 'import json,sys
try: print(json.loads(sys.stdin.read())["choices"][0]["message"]["content"].strip()[:60])
except Exception: pass' <<<"$body")
  if [[ -n "$answer" ]]; then
    echo "   OK за $(( $(date +%s) - started )) с, ответ: $answer"; ok+=("$m")
  else
    echo "   ОШИБКА: ${body:0:300}"; bad+=("$m")
  fi
done

echo
echo "Работают: ${ok[*]:-нет}"
[[ ${#bad[@]} -gt 0 ]] && echo "Не работают: ${bad[*]} — смотри: ${COMPOSE[*]} logs llm | tail -50"
[[ ${#ok[@]} -gt 0 ]] && echo "Для экспериментов: --teachers $(IFS=,; echo "${ok[*]}")"
