-- Dodorassik — initial schema
-- Equivalent to the EF Core 'Initial' migration. Regenerate with
--     dotnet ef migrations script --idempotent --output db/init.sql
-- whenever the DbContext changes. Apply with:
--     psql "$DODORASSIK_CONN" -f db/init.sql

BEGIN;

CREATE TABLE IF NOT EXISTS "Families" (
    "Id"              uuid         NOT NULL PRIMARY KEY,
    "Name"            varchar(128) NOT NULL,
    "CreatedAtUtc"    timestamptz  NOT NULL
);

CREATE TABLE IF NOT EXISTS "Users" (
    "Id"              uuid         NOT NULL PRIMARY KEY,
    "Email"           varchar(256) NOT NULL,
    "DisplayName"     varchar(128) NOT NULL,
    "PasswordHash"    varchar(512) NOT NULL,
    "Role"            integer      NOT NULL,
    "CreatedAtUtc"    timestamptz  NOT NULL,
    "LastLoginUtc"    timestamptz  NULL,
    "FamilyId"        uuid         NULL REFERENCES "Families"("Id") ON DELETE SET NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Email" ON "Users" ("Email");

CREATE TABLE IF NOT EXISTS "Hunts" (
    "Id"              uuid          NOT NULL PRIMARY KEY,
    "CreatorId"       uuid          NOT NULL REFERENCES "Users"("Id") ON DELETE RESTRICT,
    "Name"            varchar(256)  NOT NULL,
    "Description"     varchar(4000) NOT NULL,
    "CoverImageUrl"   text          NULL,
    "LocationLabel"   text          NULL,
    "Status"          integer       NOT NULL,
    "Mode"            integer       NOT NULL,
    "Category"        integer       NOT NULL DEFAULT 0,
    "EventStartUtc"   timestamptz   NULL,
    "EventEndUtc"     timestamptz   NULL,
    "CreatedAtUtc"    timestamptz   NOT NULL,
    "UpdatedAtUtc"    timestamptz   NOT NULL
);

CREATE TABLE IF NOT EXISTS "HuntSteps" (
    "Id"              uuid          NOT NULL PRIMARY KEY,
    "HuntId"          uuid          NOT NULL REFERENCES "Hunts"("Id") ON DELETE CASCADE,
    "Order"           integer       NOT NULL,
    "Title"           varchar(256)  NOT NULL,
    "Description"     varchar(4000) NOT NULL,
    "Type"            integer       NOT NULL,
    "ParamsJson"      jsonb         NOT NULL,
    "BlocksNext"      boolean       NOT NULL,
    "Points"          integer       NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_HuntSteps_HuntId_Order" ON "HuntSteps" ("HuntId", "Order");

CREATE TABLE IF NOT EXISTS "Clues" (
    "Id"              uuid          NOT NULL PRIMARY KEY,
    "HuntId"          uuid          NOT NULL REFERENCES "Hunts"("Id") ON DELETE CASCADE,
    "Code"            varchar(64)   NOT NULL,
    "Title"           varchar(256)  NOT NULL,
    "Reveal"          text          NOT NULL,
    "Points"          integer       NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Clues_HuntId_Code" ON "Clues" ("HuntId", "Code");

CREATE TABLE IF NOT EXISTS "Submissions" (
    "Id"                    uuid        NOT NULL PRIMARY KEY,
    "HuntStepId"            uuid        NOT NULL REFERENCES "HuntSteps"("Id") ON DELETE CASCADE,
    "FamilyId"              uuid        NOT NULL,
    "SubmittedById"         uuid        NOT NULL,
    "Accepted"              boolean     NOT NULL,
    "AwardedPoints"         integer     NOT NULL,
    "PayloadJson"           jsonb       NOT NULL,
    "ClientCreatedAtUtc"    timestamptz NOT NULL,
    "ServerReceivedAtUtc"   timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_Submissions_HuntStepId_FamilyId" ON "Submissions" ("HuntStepId", "FamilyId");

CREATE TABLE IF NOT EXISTS "HuntScores" (
    "Id"              uuid        NOT NULL PRIMARY KEY,
    "HuntId"          uuid        NOT NULL REFERENCES "Hunts"("Id") ON DELETE CASCADE,
    "FamilyId"        uuid        NOT NULL REFERENCES "Families"("Id") ON DELETE CASCADE,
    "TotalPoints"     integer     NOT NULL,
    "StepsCompleted"  integer     NOT NULL,
    "StartedAtUtc"    timestamptz NULL,
    "FinishedAtUtc"   timestamptz NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_HuntScores_HuntId_FamilyId" ON "HuntScores" ("HuntId", "FamilyId");

COMMIT;
