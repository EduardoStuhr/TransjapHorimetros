#!/usr/bin/env bash
set -euo pipefail
: "${SQL_CONTAINER:?container local da CI}" "${BASE_SHA:?commit base}" "${TEST_SQLSERVER_CONNECTION_STRING:?}"
[[ "$TEST_SQLSERVER_CONNECTION_STRING" == 'Server=127.0.0.1,1433;Database=transjap_horimetros_tests;'* ]]
sql() {
    docker exec -i "$SQL_CONTAINER" bash -ec 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -y 0' < "$1"
}
sql scripts/validation/fase15_recovery.sql
sql scripts/validation/fase15_verify_restore.sql
temp_dir=$(mktemp -d)
git worktree add --detach "$temp_dir/base" "$BASE_SHA"
dotnet publish "$temp_dir/base/apps/api/src/TransjapHorimetros.Api" -c Release -o "$temp_dir/base-api"
export ConnectionStrings__DefaultConnection="${TEST_SQLSERVER_CONNECTION_STRING/transjap_horimetros_tests/fase15_recovery_tests}"
export ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://127.0.0.1:4328
export Authentication__Jwt__SigningKey="$(openssl rand -hex 32)"
export Ocr__BackgroundProcessingEnabled=false Database__ApplyMigrationsOnStartup=false
# A database absent from the disposable instance must stay absent even with a stale override.
current_api="$PWD/apps/api/src/TransjapHorimetros.Api/bin/Release/net10.0"
(
    cd "$current_api"
    export ConnectionStrings__DefaultConnection="${TEST_SQLSERVER_CONNECTION_STRING/transjap_horimetros_tests/fase15_absent_tests}"
    export Database__ApplyMigrationsOnStartup=true
    exec dotnet TransjapHorimetros.Api.dll
) >"$temp_dir/guard.log" 2>&1 &
api_pid=$!
trap 'kill "$api_pid" 2>/dev/null || true' EXIT
curl --retry 10 --retry-all-errors --retry-delay 1 -fsS "$ASPNETCORE_URLS/health/live"
kill "$api_pid"
wait "$api_pid" || true
trap - EXIT
printf "%s\n" "IF DB_ID('fase15_absent_tests') IS NOT NULL THROW 51003, 'Unexpected startup migration.', 1;" > "$temp_dir/check.sql"
sql "$temp_dir/check.sql"

for api_dir in "$PWD/apps/api/src/TransjapHorimetros.Api/bin/Release/net10.0" "$temp_dir/base-api"; do
    (cd "$api_dir" && exec dotnet TransjapHorimetros.Api.dll) >"$temp_dir/server.log" 2>&1 &
    api_pid=$!
    trap 'kill "$api_pid" 2>/dev/null || true' EXIT
    curl --retry 20 --retry-all-errors --retry-delay 1 -fsS "$ASPNETCORE_URLS/health/ready"
    test "$(curl -s -o /dev/null -w '%{http_code}' "$ASPNETCORE_URLS/api/v1/readings")" = 401
    kill "$api_pid"
    wait "$api_pid" || true
    trap - EXIT
    sql scripts/validation/fase15_verify_restore.sql
done
sql scripts/validation/fase15_plans.sql > fase15-query-plans.txt
