# PgBouncer connection pooler for Railway
#
# ── Required environment variables ──────────────────────────────────────────
#
#   DATABASE_URL   Full PostgreSQL URL of the upstream database.
#                  Example: postgresql://user:pass@host:5432/dbname
#                  Alternatively, set the individual variables below:
#
#   DB_HOST        Upstream PostgreSQL hostname
#   DB_PORT        Upstream PostgreSQL port  (default: 5432)
#   DB_USER        PostgreSQL username
#   DB_PASSWORD    PostgreSQL password
#   DB_NAME        PostgreSQL database name
#
# ── Optional tuning ─────────────────────────────────────────────────────────
#
#   POOL_MODE          transaction | session | statement  (default: transaction)
#   MAX_CLIENT_CONN    Maximum client connections         (default: 1000)
#   DEFAULT_POOL_SIZE  Server connections per user/db     (default: 20)
#   SERVER_TLS_SSLMODE TLS mode to upstream server        (default: require)
#   LISTEN_PORT        Port PgBouncer listens on          (default: 5432)
#
# ── Railway setup ───────────────────────────────────────────────────────────
#
#   1. Add a new Railway Service for this folder (Docker deploy).
#   2. Set DATABASE_URL as a reference variable pointing to your Postgres
#      service: ${{Postgres.DATABASE_URL}}
#   3. In the API service, set DATABASE_URL to point to this PgBouncer service
#      instead of Postgres directly:
#        postgresql://${{PgBouncer.RAILWAY_PRIVATE_DOMAIN}}:5432/dbname
#   4. Deploy — PgBouncer will start transaction-mode pooling automatically.

FROM alpine:3.20

RUN apk add --no-cache pgbouncer \
    && (getent group pgbouncer  > /dev/null 2>&1 || addgroup -S pgbouncer) \
    && (getent passwd pgbouncer > /dev/null 2>&1 || adduser  -S -G pgbouncer -H -h /var/run/pgbouncer pgbouncer) \
    && mkdir -p /etc/pgbouncer /var/log/pgbouncer /var/run/pgbouncer \
    && chown -R pgbouncer:pgbouncer /etc/pgbouncer /var/log/pgbouncer /var/run/pgbouncer

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 5432

ENTRYPOINT ["/entrypoint.sh"]
