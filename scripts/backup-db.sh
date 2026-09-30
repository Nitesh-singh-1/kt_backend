#!/usr/bin/env bash
# Nightly Postgres backup for KTransport. Dumps via `docker exec` (no network
# exposure needed — pg_dump runs inside the postgres container itself), gzips,
# timestamps, and prunes anything older than RETENTION_DAYS.
#
# Deployed to /opt/ktransport/scripts/backup-db.sh on the VPS and run via cron
# (see crontab -l on the VPS, or re-install with the command at the bottom of
# this file). Reads DB name/user from the same /opt/ktransport/.env compose
# already uses, so it never drifts from the actual running credentials.
#
# Layer 1 of 2 (local disk only). Layer 2 (offsite copy — object storage or a
# second host) is a deliberate follow-up, not yet wired in — until it exists,
# this is the ONLY copy and does not protect against loss of the VPS itself.
set -euo pipefail

BACKUP_DIR="/opt/ktransport/backups"
RETENTION_DAYS=14
TIMESTAMP=$(date +%Y-%m-%d_%H%M%S)
DUMP_FILE="${BACKUP_DIR}/kt_${TIMESTAMP}.sql.gz"

set -a
source /opt/ktransport/.env
set +a

mkdir -p "$BACKUP_DIR"

echo "[$(date -Is)] Starting backup -> $DUMP_FILE"
docker exec kt_postgres pg_dump -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DB:-kt}" \
  | gzip > "$DUMP_FILE"

# Sanity check: a valid gzip of a real dump should be non-trivially sized and
# pass gzip's own integrity test. Catches a silently-empty/corrupt dump before
# it displaces a good backup in rotation.
if [ ! -s "$DUMP_FILE" ] || ! gzip -t "$DUMP_FILE" 2>/dev/null; then
  echo "[$(date -Is)] ERROR: backup file missing, empty, or corrupt — leaving it for inspection, not rotating old backups this run." >&2
  exit 1
fi

DUMP_SIZE=$(du -h "$DUMP_FILE" | cut -f1)
echo "[$(date -Is)] Backup OK: $DUMP_FILE ($DUMP_SIZE)"

find "$BACKUP_DIR" -name 'kt_*.sql.gz' -type f -mtime "+${RETENTION_DAYS}" -print -delete

echo "[$(date -Is)] Done. $(find "$BACKUP_DIR" -name 'kt_*.sql.gz' | wc -l) backups retained."

# --- Install / re-install on the VPS ---
# scp this file to /opt/ktransport/scripts/backup-db.sh, chmod 700, then:
#   (crontab -l 2>/dev/null | grep -v backup-db.sh; \
#    echo '30 20 * * * /opt/ktransport/scripts/backup-db.sh >> /opt/ktransport/backups/backup.log 2>&1  # 2:00 AM IST (UTC+5:30)') \
#    | crontab -
# The VPS runs in UTC (`timedatectl`) — 30 20 UTC = 2:00 AM IST, chosen to land
# outside Indian business hours.
