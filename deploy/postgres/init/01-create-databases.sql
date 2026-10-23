-- One Postgres instance, one database (`azma`), schema-per-service.
-- Migrations per-service create their own schema; we only ensure they exist.

CREATE SCHEMA IF NOT EXISTS identity;
CREATE SCHEMA IF NOT EXISTS projects;
CREATE SCHEMA IF NOT EXISTS testcases;
CREATE SCHEMA IF NOT EXISTS runs;
CREATE SCHEMA IF NOT EXISTS reporting;

-- Helpful extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
CREATE EXTENSION IF NOT EXISTS "citext";
