#!/bin/sh
# Generates pgbouncer.ini and userlist.txt from environment variables,
# then starts PgBouncer. Supports both DATABASE_URL and individual variables.
set -eu

# ── Parse DATABASE_URL if provided ──────────────────────────────────────────
if [ -n "${DATABASE_URL:-}" ]; then
    # Strip scheme (postgresql:// or postgres://)
    rest="${DATABASE_URL#*://}"
    userinfo="${rest%%@*}"
    hostdb="${rest#*@}"

    DB_USER="${userinfo%%:*}"
    DB_PASSWORD="${userinfo#*:}"

    hostport="${hostdb%%/*}"
    DB_HOST="${hostport%%:*}"
    portpart="${hostport#*:}"
    # If there was no colon in hostport, portpart equals hostport
    if [ "$portpart" = "$hostport" ]; then
        DB_PORT="${DB_PORT:-5432}"
    else
        DB_PORT="$portpart"
    fi

    DB_NAME="${hostdb#*/}"
    # Strip query string from DB_NAME if present
    DB_NAME="${DB_NAME%%\?*}"
fi

: "${DB_HOST:?DB_HOST or DATABASE_URL must be set}"
: "${DB_USER:?DB_USER or DATABASE_URL must be set}"
: "${DB_PASSWORD:?DB_PASSWORD or DATABASE_URL must be set}"
: "${DB_NAME:?DB_NAME or DATABASE_URL must be set}"
: "${DB_PORT:=5432}"

# Tuning knobs with sensible defaults
POOL_MODE="${POOL_MODE:-transaction}"
MAX_CLIENT_CONN="${MAX_CLIENT_CONN:-1000}"
DEFAULT_POOL_SIZE="${DEFAULT_POOL_SIZE:-20}"
SERVER_TLS_SSLMODE="${SERVER_TLS_SSLMODE:-require}"
LISTEN_PORT="${LISTEN_PORT:-5432}"

# ── Generate userlist.txt (MD5 auth) ────────────────────────────────────────
# PgBouncer expects: "username" "md5<md5(password+username)>"
MD5HASH=$(printf '%s' "${DB_PASSWORD}${DB_USER}" | md5sum | cut -d' ' -f1)
printf '"%s" "md5%s"\n' "$DB_USER" "$MD5HASH" > /etc/pgbouncer/userlist.txt

# ── Generate pgbouncer.ini ───────────────────────────────────────────────────
cat > /etc/pgbouncer/pgbouncer.ini << EOF
[databases]
* = host=${DB_HOST} port=${DB_PORT} dbname=${DB_NAME}

[pgbouncer]
listen_addr          = 0.0.0.0
listen_port          = ${LISTEN_PORT}
auth_type            = md5
auth_file            = /etc/pgbouncer/userlist.txt

pool_mode            = ${POOL_MODE}
max_client_conn      = ${MAX_CLIENT_CONN}
default_pool_size    = ${DEFAULT_POOL_SIZE}

; DISCARD ALL resets session state between transactions (required for transaction pooling)
server_reset_query   = DISCARD ALL

; TLS to the upstream PostgreSQL server
server_tls_sslmode   = ${SERVER_TLS_SSLMODE}

; Suppress harmless parameter mismatch warnings from some clients
ignore_startup_parameters = extra_float_digits

logfile  = /var/log/pgbouncer/pgbouncer.log
pidfile  = /var/run/pgbouncer/pgbouncer.pid
EOF

# ── Ensure runtime directories exist and are writable ───────────────────────
mkdir -p /var/log/pgbouncer /var/run/pgbouncer
chown -R pgbouncer:pgbouncer \
    /var/log/pgbouncer \
    /var/run/pgbouncer \
    /etc/pgbouncer

exec pgbouncer /etc/pgbouncer/pgbouncer.ini
