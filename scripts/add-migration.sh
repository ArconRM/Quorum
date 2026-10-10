#!/usr/bin/env bash
# Генерация EF Core миграций в src/<Service>/<Service>.Infrastructure/Db/Migrations.
#   ./scripts/add-migration.sh Initial                 # для всех сервисов
#   ./scripts/add-migration.sh AddFrameTags Datasets   # для одного
set -euo pipefail
cd "$(dirname "$0")/.."
NAME=${1:?"usage: add-migration.sh <MigrationName> [Service ...]"}
shift
SERVICES=${*:-"Identity Datasets Labeling Training"}

dotnet tool restore
for S in $SERVICES; do
  echo "==> $S"
  dotnet ef migrations add "$NAME" \
    --project "src/$S/$S.Infrastructure" \
    --startup-project "src/$S/$S.Service" \
    --output-dir Db/Migrations
done
