-- Migration 001: initial schema
-- Uses UUID v7 PKs (generated in C#) with gen_random_uuid() as DB-side fallback.
-- Invoices are partitioned by RANGE on "FechaEmision" for future scalability.
-- Table and column names match EF Core's default PascalCase quoting convention.

-- Required for gen_random_uuid() on PostgreSQL < 13
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ── Tenants ───────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS "Tenants" (
    "Id"        UUID        NOT NULL DEFAULT gen_random_uuid(),
    "Name"      TEXT        NOT NULL,
    "Slug"      TEXT        NOT NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Tenants_Slug" ON "Tenants" ("Slug");

-- ── Users ─────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS "Users" (
    "Id"           UUID        NOT NULL DEFAULT gen_random_uuid(),
    "TenantId"     UUID        NOT NULL REFERENCES "Tenants" ("Id"),
    "Email"        TEXT        NOT NULL,
    "PasswordHash" TEXT        NOT NULL,
    "Name"         TEXT        NOT NULL,
    "Role"         TEXT        NOT NULL DEFAULT 'user',
    "CreatedAt"    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Email"    ON "Users" ("Email");
CREATE        INDEX IF NOT EXISTS "IX_Users_TenantId" ON "Users" ("TenantId");

-- ── Invoices (partitioned by FechaEmision) ────────────────────────────────────
-- PostgreSQL requires the partition key to be part of the primary key.
-- EF Core is configured with HasKey(i => new { i.Id, i.FechaEmision }) to match.

CREATE TABLE IF NOT EXISTS "Invoices" (
    "Id"                  UUID          NOT NULL DEFAULT gen_random_uuid(),
    "TenantId"            UUID          NOT NULL,
    "TipoCfe"             INTEGER       NOT NULL,
    "Numero"              BIGINT        NOT NULL,
    "FechaEmision"        TIMESTAMPTZ   NOT NULL,
    "RutReceptor"         TEXT,
    "NombreReceptor"      TEXT,
    "MontoTotal"          NUMERIC(18,2) NOT NULL DEFAULT 0,
    "MontoNetoExento"     NUMERIC(18,2) NOT NULL DEFAULT 0,
    "MontoNetoMinimo"     NUMERIC(18,2) NOT NULL DEFAULT 0,
    "MontoNetoBasico"     NUMERIC(18,2) NOT NULL DEFAULT 0,
    "IvaMinimo"           NUMERIC(18,2) NOT NULL DEFAULT 0,
    "IvaBasico"           NUMERIC(18,2) NOT NULL DEFAULT 0,
    "AceptadoPorDgi"      BOOLEAN       NOT NULL DEFAULT false,
    "CodigoRespuestaDgi"  TEXT,
    "MensajeRespuestaDgi" TEXT,
    "XmlFirmado"          TEXT,
    "DetalleJson"         TEXT,
    "R2PdfKey"            TEXT,
    "R2XmlKey"            TEXT,
    PRIMARY KEY ("Id", "FechaEmision")
) PARTITION BY RANGE ("FechaEmision");

-- Annual partitions — add a new one each year or automate with pg_partman.
CREATE TABLE IF NOT EXISTS "Invoices_2024" PARTITION OF "Invoices"
    FOR VALUES FROM ('2024-01-01') TO ('2025-01-01');

CREATE TABLE IF NOT EXISTS "Invoices_2025" PARTITION OF "Invoices"
    FOR VALUES FROM ('2025-01-01') TO ('2026-01-01');

CREATE TABLE IF NOT EXISTS "Invoices_2026" PARTITION OF "Invoices"
    FOR VALUES FROM ('2026-01-01') TO ('2027-01-01');

CREATE TABLE IF NOT EXISTS "Invoices_2027" PARTITION OF "Invoices"
    FOR VALUES FROM ('2027-01-01') TO ('2028-01-01');

-- Default partition catches any rows outside the explicit ranges.
CREATE TABLE IF NOT EXISTS "Invoices_default" PARTITION OF "Invoices" DEFAULT;

-- Composite index on tenant + date; used by nearly every query.
CREATE INDEX IF NOT EXISTS "IX_Invoices_TenantId_FechaEmision"
    ON "Invoices" ("TenantId", "FechaEmision" DESC);
