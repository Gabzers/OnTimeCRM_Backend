# OnTimeCRM — SQL Directory

Standalone SQL files extracted from `DatabaseFunctions.cs` for maintainability.
The application still applies every function automatically at startup via `DatabaseInitializer.cs`.
These files are useful for documentation, DBA review, manual patching, and local setup.

---

## Directory Structure

```
sql/
├── 00_schema.sql              ← master script (run this for a fresh DB)
├── 01_tables.sql              ← DDL for all 16 tables (IF NOT EXISTS)
└── functions/
    ├── 02_auth.sql            ← authentication & registration
    ├── 03_clients.sql         ← client pipeline (paged, stage change, delete)
    ├── 04_proposals.sql       ← proposals, loss marking, sale conversion
    ├── 05_sales.sql           ← sales read operations
    ├── 06_dashboard.sql       ← KPIs, hot deals, monthly stats, loss reasons
    ├── 07_notifications.sql   ← notification CRUD + preferences
    ├── 08_stages.sql          ← stage & template management
    ├── 09_users.sql           ← user profile & activation
    ├── 10_vehicles.sql        ← vehicle brand/model catalogue
    └── 11_brands.sql          ← brand management
```

---

## Naming Convention

All stored functions follow `fn_<verb>_<resource>[_qualifier]`:

| Verb prefix | Meaning |
|-------------|---------|
| `fn_get_`   | SELECT (read) |
| `fn_create_`| INSERT (returns new ID) |
| `fn_update_`| UPDATE (returns VOID or new value) |
| `fn_delete_`| DELETE (returns VOID or BOOLEAN if guarded) |
| `fn_set_`   | Toggle / single-field update |
| `fn_mark_`  | Status transition (e.g. `fn_mark_proposal_lost`) |
| `fn_convert_`| Complex transformation (e.g. `fn_convert_proposal_to_sale`) |
| `fn_reorder_`| Bulk order update |

---

## Fresh Database Setup

### Option A — Docker (local dev)

```bash
# Start PostgreSQL 16 at localhost:5434
docker-compose up -d postgres

# Create the database (first time only)
docker exec -it ontimecrm_postgres psql -U postgres -c "CREATE DATABASE ontimecrm;"

# Apply full schema
docker exec -i ontimecrm_postgres psql -U postgres -d ontimecrm \
  < sql/00_schema.sql
```

### Option B — Supabase / existing PostgreSQL

```bash
psql "postgresql://postgres:<password>@<host>:5432/postgres?sslmode=require" \
  -f sql/00_schema.sql
```

> **Note:** The .NET application also runs `EnsureCreated()` + re-applies all
> functions at startup, so you typically don't need to run these scripts manually.
> Use them for: initial provisioning, CI pipelines, DBA review, or manual hotfixes.

---

## Re-applying a Single Function File

```bash
# Re-apply only notification functions (e.g. after modifying 07_notifications.sql)
psql -U postgres -d ontimecrm -f sql/functions/07_notifications.sql
```

All functions use `CREATE OR REPLACE`, so re-running is always safe.

---

## Connection Details

| Environment | Host | Port | Database | User |
|-------------|------|------|----------|------|
| Local dev   | localhost | 5434 | ontimecrm | postgres |
| Production  | Supabase connection string in `ASPNETCORE_ConnectionStrings__DefaultConnection` |

### Required Extension

```sql
CREATE EXTENSION IF NOT EXISTS pgcrypto;  -- provides gen_random_uuid()
```

This is pre-installed on Supabase and available by default on PostgreSQL ≥ 13.

---

## Key Design Notes

- **`"order"` is a PostgreSQL reserved word.** The `client_stages."order"` column must always be double-quoted in raw SQL. EF Core's `UseSnakeCaseNamingConvention()` handles this automatically in C#.
- **`sold_at` and `proposal_date` are business dates, not system timestamps.** They are always supplied by the user and must never be auto-set to `NOW()`.
- **Enums are stored as `SMALLINT`** (not strings) for storage efficiency on Supabase free tier.
- **All lists are paginated** via `OFFSET`/`LIMIT` with `COUNT(*) OVER()` for total count. Default page size: 20, max: 50.
- **Temperature** (`hot`/`warm`/`cold`) is recalculated on every stage change, not stored as a stale value. The `fn_update_client_stage` function handles this inline.
