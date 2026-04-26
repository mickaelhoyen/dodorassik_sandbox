-- Migration: add Category, EventStartUtc, EventEndUtc to Hunts
-- Apply to existing databases that already have the initial schema:
--     psql "$DODORASSIK_CONN" -f db/migrate_add_hunt_category.sql
-- Safe to run multiple times (IF NOT EXISTS / DEFAULT guards).

BEGIN;

ALTER TABLE "Hunts"
    ADD COLUMN IF NOT EXISTS "Category"      integer     NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "EventStartUtc" timestamptz NULL,
    ADD COLUMN IF NOT EXISTS "EventEndUtc"   timestamptz NULL;

COMMIT;
