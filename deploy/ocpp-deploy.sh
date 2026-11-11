#!/usr/bin/env bash
# Fetches the latest successful "build-pi.yml" run on main and deploys both
# artifacts (ocpp-server, ocpp-mgmt). Started every minute by ocpp-deploy.timer.
# DRY_RUN=1 downloads and checks the build, but deploys nothing.
set -euo pipefail

REPO="binkycode/OCPP.Core"
WORKFLOW="build-pi.yml"
APP_ROOT="/home/frederic/OCPP"
DB="/mnt/data/ocpp/OCPP.Core.sqlite"
STATE_DIR="/home/frederic/.local/state/ocpp-deploy"
ENV_FILE="/home/frederic/.config/ocpp-deploy.env"
DRY_RUN="${DRY_RUN:-0}"

# artifact name | app subdir | executable | systemd service | health URL
APPS=(
    "ocpp-server|server|OCPP.Core.Server|OcppServer|http://127.0.0.1:8081/"
    "ocpp-mgmt|mgmt|OCPP.Core.Management|OcppManagement|http://127.0.0.1:8082/Account/Login"
)

log() { echo "$(date '+%F %T') $*"; }

# shellcheck source=/dev/null
source "$ENV_FILE"
: "${GITHUB_TOKEN:?GITHUB_TOKEN ontbreekt in $ENV_FILE}"

mkdir -p "$STATE_DIR"
exec 9>"$STATE_DIR/lock"
flock -n 9 || { log "Vorige deploy loopt nog"; exit 0; }

api() {
    curl -fsS --max-time 30 \
        -H "Authorization: Bearer $GITHUB_TOKEN" \
        -H "Accept: application/vnd.github+json" \
        -H "X-GitHub-Api-Version: 2022-11-28" \
        "https://api.github.com/repos/$REPO/$1"
}

healthy() {
    local url="$1" code
    for _ in $(seq 1 30); do
        code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$url" || true)
        [ "$code" = "200" ] && return 0
        sleep 3
    done
    return 1
}

stop_all()  { sudo -n /usr/bin/systemctl stop OcppManagement; sudo -n /usr/bin/systemctl stop OcppServer; }
start_all() { sudo -n /usr/bin/systemctl start OcppServer; sudo -n /usr/bin/systemctl start OcppManagement; }

all_healthy() {
    local entry name dir exe svc url
    for entry in "${APPS[@]}"; do
        IFS='|' read -r name dir exe svc url <<< "$entry"
        healthy "$url" || { log "$svc antwoordt niet op $url"; return 1; }
    done
}

read -r run_id head_sha < <(
    api "actions/workflows/$WORKFLOW/runs?branch=main&event=push&status=success&per_page=1" |
        python3 -c 'import json, sys; r = json.load(sys.stdin)["workflow_runs"]; print(r[0]["id"], r[0]["head_sha"]) if r else print("- -")'
)
[ -n "${run_id:-}" ] || { log "GitHub API niet bereikbaar of token ongeldig"; exit 1; }
[ "$run_id" != "-" ] || { log "Nog geen geslaagde build op main"; exit 0; }

deployed_sha=$(cat "$STATE_DIR/deployed-sha" 2>/dev/null || true)
failed_sha=$(cat "$STATE_DIR/failed-sha" 2>/dev/null || true)
[ "$head_sha" = "$deployed_sha" ] && exit 0
[ "$head_sha" = "$failed_sha" ] && exit 0

log "Nieuwe build: run $run_id, commit ${head_sha:0:7} (huidige: ${deployed_sha:0:7})"

artifacts_json=$(api "actions/runs/$run_id/artifacts")
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

for entry in "${APPS[@]}"; do
    IFS='|' read -r name dir exe svc url <<< "$entry"
    artifact_url=$(
        python3 -c 'import json, sys
for a in json.loads(sys.argv[2])["artifacts"]:
    if a["name"] == sys.argv[1] and not a["expired"]:
        print(a["archive_download_url"])
        break' "$name" "$artifacts_json"
    )
    if [ -z "$artifact_url" ]; then
        log "Artefact '$name' niet gevonden of verlopen; commit ${head_sha:0:7} wordt overgeslagen"
        echo "$head_sha" > "$STATE_DIR/failed-sha"
        exit 1
    fi
    curl -fsSL --max-time 600 -H "Authorization: Bearer $GITHUB_TOKEN" -o "$work/$name.zip" "$artifact_url"
    unzip -q "$work/$name.zip" -d "$work/$dir"
    if [ ! -f "$work/$dir/$exe" ]; then
        log "Artefact '$name' bevat geen $exe; commit ${head_sha:0:7} wordt overgeslagen"
        echo "$head_sha" > "$STATE_DIR/failed-sha"
        exit 1
    fi
    chmod +x "$work/$dir/$exe"
done

if [ "$DRY_RUN" = "1" ]; then
    log "Droge run: build ${head_sha:0:7} is in orde ($(du -sh "$work" | cut -f1)), er is niets gedeployed"
    exit 0
fi

# Full copy of the current version (including appsettings.Production.json) for a rollback.
mkdir -p "$STATE_DIR/previous"
rsync -a --delete "$APP_ROOT/" "$STATE_DIR/previous/"

stop_all

# The Server may migrate the database on startup; take a copy while both apps are stopped.
cp "$DB" "$STATE_DIR/previous.sqlite" 2>/dev/null || true

for entry in "${APPS[@]}"; do
    IFS='|' read -r name dir exe svc url <<< "$entry"
    rsync -a "$work/$dir/" "$APP_ROOT/$dir/"
done
start_all

if all_healthy; then
    echo "$head_sha" > "$STATE_DIR/deployed-sha"
    rm -f "$STATE_DIR/failed-sha"
    log "Deploy van ${head_sha:0:7} geslaagd"
else
    log "Deploy van ${head_sha:0:7} mislukt; vorige versie en database worden teruggezet"
    stop_all
    rsync -a --delete "$STATE_DIR/previous/" "$APP_ROOT/"
    if [ -f "$STATE_DIR/previous.sqlite" ]; then
        cp "$STATE_DIR/previous.sqlite" "$DB"
        rm -f "$DB-wal" "$DB-shm"
    fi
    start_all
    echo "$head_sha" > "$STATE_DIR/failed-sha"
    if all_healthy; then log "Vorige versie draait weer"; else log "Ook de vorige versie antwoordt niet!"; fi
    exit 1
fi
