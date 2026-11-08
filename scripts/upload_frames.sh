#!/usr/bin/env bash
# Эмуляция камеры: заливает кадры из папки в камеру через API Gateway, как будто они приходят с устройства.
#
#   ./scripts/upload_frames.sh <cameraId> <папка> [пауза_сек=0] [лимит] [gateway=http://localhost:8000]
#
# Пример — 77 тысяч кадров курсовой 3 курса, по одному кадру в секунду, первые 500:
#   ./scripts/upload_frames.sh 3f2a… ~/data/camera_window 1 500
#
# Логин берётся из ADMIN_EMAIL / ADMIN_PASSWORD (по умолчанию — администратор из docker-compose).
# Кадры идут в порядке имён: у потока из курсовой в имени unix-время, так сохраняется хронология.
set -euo pipefail

CAMERA=${1:?cameraId}
DIR=${2:?папка с кадрами}
DELAY=${3:-0}
LIMIT=${4:-0}
GW=${5:-http://localhost:8000}

TOKEN=$(curl -sf -X POST "$GW/identity/auth/login" -H "Content-Type: application/json" \
  -d "{\"email\":\"${ADMIN_EMAIL:-admin@quorum.local}\",\"password\":\"${ADMIN_PASSWORD:-Admin123!}\"}" | jq -r .token)
[[ -n "$TOKEN" && "$TOKEN" != null ]] || { echo "Не удалось войти в $GW" >&2; exit 1; }

sent=0 failed=0
while IFS= read -r -d '' f; do
  case "${f,,}" in
    *.jpg|*.jpeg) type=image/jpeg ;;
    *.png) type=image/png ;;
    *) continue ;;
  esac
  if curl -sf -X POST "$GW/datasets/cameras/$CAMERA/frames" -H "Authorization: Bearer $TOKEN" \
       -F "file=@$f;type=$type" >/dev/null; then
    sent=$((sent + 1))
  else
    failed=$((failed + 1)); echo "! не загрузился: $f" >&2
  fi
  (( sent % 100 == 0 && sent > 0 )) && echo "отправлено: $sent"
  (( LIMIT > 0 && sent >= LIMIT )) && break
  [[ "$DELAY" != 0 ]] && sleep "$DELAY"
done < <(find "$DIR" -type f -print0 | sort -z)

echo "Готово: отправлено $sent, ошибок $failed"
curl -sf "$GW/datasets/cameras/$CAMERA" -H "Authorization: Bearer $TOKEN" | jq -c '{name, newFrames}'
