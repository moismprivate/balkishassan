#!/usr/bin/env bash
set -Eeuo pipefail

# PostgreSQL-authenticatie hoort in ~/.pgpass (modus 0600), niet in dit script.
: "${PGHOST:=127.0.0.1}"
: "${PGPORT:=5432}"
: "${PGDATABASE:=balkishassan}"
: "${PGUSER:=balkishassan}"
: "${BACKUP_DIR:=/var/backups/balkishassan}"
: "${UPLOAD_DIR:=/var/lib/balkishassan/uploads}"
: "${RETENTION_DAYS:=14}"
export PGHOST PGPORT PGDATABASE PGUSER

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
install -d -m 0700 "${BACKUP_DIR}"
pg_dump --format=custom --file="${BACKUP_DIR}/database-${stamp}.dump"
tar --create --gzip --file="${BACKUP_DIR}/uploads-${stamp}.tar.gz" -C "$(dirname "${UPLOAD_DIR}")" "$(basename "${UPLOAD_DIR}")"
sha256sum "${BACKUP_DIR}/database-${stamp}.dump" "${BACKUP_DIR}/uploads-${stamp}.tar.gz" > "${BACKUP_DIR}/checksums-${stamp}.sha256"

find "${BACKUP_DIR}" -maxdepth 1 -type f -mtime "+${RETENTION_DAYS}" \
  \( -name 'database-*.dump' -o -name 'uploads-*.tar.gz' -o -name 'checksums-*.sha256' \) -delete
