#!/usr/bin/env bash
# Nightly backup of the OCPP.Core database (same approach as Huishoudboek).
#
# - Consistent copy via the SQLite backup API (safe while the apps are running).
# - Checked with PRAGMA integrity_check before it is kept.
# - Kept compressed on the USB stick and on the SD card (in case the stick dies).
# - 14 daily and 12 monthly copies.
# - age-encrypted copy on OneDrive when the Huishoudboek rclone remote and recipient
#   key are present (30 daily, 12 monthly), in a separate folder.
set -euo pipefail

DB="${OCPP_DB:-/mnt/data/ocpp/OCPP.Core.sqlite}"
TARGETS=("${OCPP_BACKUP_USB:-/mnt/data/ocpp/backups}" "${OCPP_BACKUP_SD:-$HOME/ocpp-backups}")
KEEP_DAILY=14
KEEP_MONTHLY=12

umask 077
today="$(date +%F)"
month="$(date +%Y-%m)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

if [ ! -f "$DB" ]; then
    echo "Database $DB niet gevonden" >&2
    exit 1
fi

python3 - "$DB" "$work/ocpp.sqlite" <<'PY'
import sqlite3, sys
src = sqlite3.connect(f"file:{sys.argv[1]}?mode=ro", uri=True)
dst = sqlite3.connect(sys.argv[2])
with dst:
    src.backup(dst)
result = dst.execute("PRAGMA integrity_check").fetchone()[0]
count = dst.execute("SELECT COUNT(*) FROM Transactions").fetchone()[0]
dst.close(); src.close()
if result != "ok":
    sys.exit(f"Integriteitscontrole mislukt: {result}")
print(f"Kopie ok, {count} transacties")
PY

gzip -9 "$work/ocpp.sqlite"

prune() {
    # Keep the newest $2 files matching pattern $1.
    local pattern="$1" keep="$2"
    ls -1 $pattern 2>/dev/null | sort -r | tail -n +"$((keep + 1))" | xargs -r rm -f
}

for dir in "${TARGETS[@]}"; do
    mkdir -p "$dir"
    chmod 700 "$dir"
    cp "$work/ocpp.sqlite.gz" "$dir/daily-$today.sqlite.gz.tmp"
    mv "$dir/daily-$today.sqlite.gz.tmp" "$dir/daily-$today.sqlite.gz"
    if [ ! -f "$dir/monthly-$month.sqlite.gz" ]; then
        cp "$dir/daily-$today.sqlite.gz" "$dir/monthly-$month.sqlite.gz"
    fi
    prune "$dir/daily-*.sqlite.gz" "$KEEP_DAILY"
    prune "$dir/monthly-*.sqlite.gz" "$KEEP_MONTHLY"
    echo "Bewaard in $dir"
done

# Encrypted off-site copy: reuses the Huishoudboek age recipient and rclone remote.
RECIPIENT="${OCPP_BACKUP_RECIPIENT:-$HOME/.config/huishoudboek/backup-recipient.txt}"
REMOTE="${OCPP_BACKUP_REMOTE:-hbonedrive:OCPP-backups}"
KEEP_REMOTE_DAILY=30

if [ -f "$RECIPIENT" ] && rclone listremotes | grep -qx "${REMOTE%%:*}:"; then
    age -R "$RECIPIENT" -o "$work/ocpp.sqlite.gz.age" "$work/ocpp.sqlite.gz"
    rclone copyto "$work/ocpp.sqlite.gz.age" "$REMOTE/daily-$today.sqlite.gz.age"
    if ! rclone lsf "$REMOTE" --include "monthly-$month.sqlite.gz.age" | grep -q .; then
        rclone copyto "$work/ocpp.sqlite.gz.age" "$REMOTE/monthly-$month.sqlite.gz.age"
    fi
    rclone lsf "$REMOTE" --include "daily-*.sqlite.gz.age" | sort -r | tail -n +"$((KEEP_REMOTE_DAILY + 1))" \
        | while read -r old; do rclone deletefile "$REMOTE/$old"; done
    rclone lsf "$REMOTE" --include "monthly-*.sqlite.gz.age" | sort -r | tail -n +"$((KEEP_MONTHLY + 1))" \
        | while read -r old; do rclone deletefile "$REMOTE/$old"; done
    echo "Versleuteld bewaard op $REMOTE"
else
    echo "Geen OneDrive-backup: $RECIPIENT of rclone-remote ${REMOTE%%:*} ontbreekt" >&2
fi
