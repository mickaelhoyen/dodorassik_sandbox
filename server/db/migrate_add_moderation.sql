-- Idempotent migration: adds the moderation workflow columns + status index.
-- Apply on a database already initialised by db/init.sql
-- (or by a previous `dotnet ef database update`):
--     psql "$DODORASSIK_CONN" -f db/migrate_add_moderation.sql

BEGIN;

ALTER TABLE "Hunts" ADD COLUMN IF NOT EXISTS "SubmittedAtUtc"  timestamptz   NULL;
ALTER TABLE "Hunts" ADD COLUMN IF NOT EXISTS "ReviewedAtUtc"   timestamptz   NULL;
ALTER TABLE "Hunts" ADD COLUMN IF NOT EXISTS "ReviewedById"    uuid          NULL;
ALTER TABLE "Hunts" ADD COLUMN IF NOT EXISTS "RejectionReason" varchar(2000) NULL;

CREATE INDEX IF NOT EXISTS "IX_Hunts_Status" ON "Hunts" ("Status");

COMMIT;
