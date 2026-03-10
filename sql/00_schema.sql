-- =============================================================================
-- OnTimeCRM — Master Schema Script
-- =============================================================================
-- Run this file to fully initialise (or re-apply) the database from scratch.
--
-- Usage:
--   psql -U postgres -d ontimecrm -f sql/00_schema.sql
--
-- Notes:
--   • Tables use IF NOT EXISTS — safe to re-run on an existing database.
--   • All functions use CREATE OR REPLACE — idempotent.
--   • Functions are applied after tables so FK constraints exist first.
--   • Run order matters: 02_auth depends on tables created in 01_tables.
-- =============================================================================

-- Required extension (available by default on PostgreSQL 13+ and Supabase)
CREATE EXTENSION IF NOT EXISTS pgcrypto;

\echo '▶  Creating tables...'
\i sql/01_tables.sql

\echo '▶  Loading auth functions...'
\i sql/functions/02_auth.sql

\echo '▶  Loading client functions...'
\i sql/functions/03_clients.sql

\echo '▶  Loading proposal functions...'
\i sql/functions/04_proposals.sql

\echo '▶  Loading sales functions...'
\i sql/functions/05_sales.sql

\echo '▶  Loading dashboard functions...'
\i sql/functions/06_dashboard.sql

\echo '▶  Loading notification functions...'
\i sql/functions/07_notifications.sql

\echo '▶  Loading stage functions...'
\i sql/functions/08_stages.sql

\echo '▶  Loading user functions...'
\i sql/functions/09_users.sql

\echo '▶  Loading vehicle functions...'
\i sql/functions/10_vehicles.sql

\echo '▶  Loading brand functions...'
\i sql/functions/11_brands.sql

\echo '✔  Schema initialisation complete.'
