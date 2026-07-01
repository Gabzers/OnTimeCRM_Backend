using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace OnTime.Infrastructure.Persistence;

/// <summary>
/// Applies EnsureCreated-equivalent schema setup at application startup. All query/aggregate
/// logic lives in C#/EF Core now — see 04-DECISIONS/2026-05-30-data-layer.md for why the project
/// moved away from raw PostgreSQL `fn_*` functions (no SQL function application step here anymore).
///
/// Schema auto-recovery: if the DB already exists but is missing tables (e.g. new entities added
/// since last deploy), the `public` schema is dropped and recreated from scratch with the current
/// model — NOT the whole database via EnsureDeleted/EnsureCreated (see below for why).
///
/// PRODUCTION SAFETY: once real customer data exists, silently wiping the schema on drift would
/// destroy it. In Production, drift makes InitializeAsync throw and the app refuses to start,
/// instead of wiping anything — loud failure over silent data loss. Adding a column/table in
/// Production from here on requires a deliberate manual step (a hand-written ALTER/CREATE run
/// directly against the DB) until the project adopts real EF migrations. See BEFORE-DEPLOY.md.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db, bool isProduction = false, CancellationToken ct = default)
    {
        var creator = (IRelationalDatabaseCreator)db.Database.GetService<IDatabaseCreator>();

        if (!await creator.ExistsAsync(ct))
        {
            // First-ever run against a database that doesn't exist yet (local docker-compose
            // Postgres, where the app owns its own dedicated database name).
            await creator.CreateAsync(ct);
            await creator.CreateTablesAsync(ct);
        }
        else
        {
            var (current, anyExpectedTableExists) = await CheckSchemaAsync(db, ct);
            if (!current)
            {
                if (!anyExpectedTableExists)
                {
                    // The database exists (managed hosts like Supabase always give you one fixed
                    // database, e.g. always named `postgres`) but none of OUR tables exist yet —
                    // first-ever deploy against this DB. Nothing to lose, safe in any environment.
                    await creator.CreateTablesAsync(ct);
                }
                else if (isProduction)
                {
                    throw new InvalidOperationException(
                        "Schema drift detected in Production, and some of our tables already " +
                        "exist — refusing to auto-wipe the `public` schema, that would destroy " +
                        "real customer data. Apply the missing table/column/index change " +
                        "manually (a hand-written SQL script run directly against the database), " +
                        "then redeploy. See BEFORE-DEPLOY.md.");
                }
                else
                {
                    // Dev/test only (see the Production guard above) — wipe and recreate just the
                    // `public` schema, not the whole database via EnsureDeleted/EnsureCreated:
                    // managed hosts don't let you DROP DATABASE the one you're connected to, and
                    // EF Core's own "has tables" heuristic scans *all* schemas, so it wrongly
                    // concludes the schema is set up when the host provisions its own schemas
                    // (Supabase always has auth/storage/realtime tables in a brand-new project).
                    await db.Database.ExecuteSqlRawAsync("DROP SCHEMA public CASCADE; CREATE SCHEMA public;", ct);
                    await creator.CreateTablesAsync(ct);
                }
            }
        }

        // One-time data cleanup, safe to re-run every startup: "/admin" used to be seeded as a
        // per-company, per-role configurable menu permission (PermissionService.AllRoutes).
        // It no longer is — cross-tenant platform-admin access is now gated to role==2 only at
        // the policy level, never a tenant-configurable permission — so any row seeded before
        // that change is stale and would otherwise keep showing a misleading "/admin" toggle in
        // the Access Control screen forever (EnsureCreated never re-seeds existing rows).
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM menu_item_permissions WHERE route_key = '/admin'", ct);
    }

    // ── Compares EF model entity tables AND columns against information_schema ─────────────
    // Generic check — every table AND every column the current EF model expects must exist.
    // This replaces a single hardcoded "sentinel column" that only caught drift if that one
    // specific column happened to be the thing that changed; a column added to any other
    // entity (e.g. UserGoal.ShowOnDashboard) silently passed the old check and caused a 500
    // at runtime instead of triggering the drop+recreate this whole mechanism exists for.
    //
    // Returns (current, anyExpectedTableExists) — the second value lets the caller tell "nothing
    // built yet, safe to create" apart from "some of our tables already exist and may hold real
    // data, drift here means don't touch it in Production".
    private static async Task<(bool current, bool anyExpectedTableExists)> CheckSchemaAsync(AppDbContext db, CancellationToken ct)
    {
        var existingTables = await db.Database
            .SqlQuery<string>($"SELECT table_name::text FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync(ct);
        var existingTableSet = existingTables.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var expectedTables = db.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(t => t is not null)
            .Select(t => t!)
            .Distinct()
            .ToList();

        var anyExpectedTableExists = expectedTables.Any(t => existingTableSet.Contains(t));
        if (!expectedTables.All(t => existingTableSet.Contains(t))) return (false, anyExpectedTableExists);

        // Snake-case naming convention is active (see AppDbContext), so the SqlQuery<T> record
        // properties below must match information_schema's own snake_case column names exactly
        // — no aliasing needed/wanted.
        var existingColumns = await db.Database
            .SqlQuery<TableColumn>(
                $"SELECT table_name, column_name FROM information_schema.columns WHERE table_schema = 'public'")
            .ToListAsync(ct);
        var existingColumnSet = existingColumns
            .Select(c => $"{c.TableName}.{c.ColumnName}".ToLowerInvariant())
            .ToHashSet();

        foreach (var entityType in db.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName is null) continue;

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName();
                if (columnName is null) continue;

                var key = $"{tableName}.{columnName}".ToLowerInvariant();
                if (!existingColumnSet.Contains(key)) return (false, anyExpectedTableExists);
            }
        }

        // EnsureCreated only ever CREATEs — it never ALTERs an existing table, so an index
        // added to the model after the DB already exists would otherwise silently never be
        // applied. Treat a missing expected index the same as a missing column: drift.
        var existingIndexNames = (await db.Database
            .SqlQuery<string>($"SELECT indexname::text FROM pg_indexes WHERE schemaname = 'public'")
            .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entityType in db.Model.GetEntityTypes())
        {
            foreach (var index in entityType.GetIndexes())
            {
                var indexName = index.GetDatabaseName();
                if (indexName is null) continue;
                if (!existingIndexNames.Contains(indexName)) return (false, anyExpectedTableExists);
            }
        }

        return (true, anyExpectedTableExists);
    }

    private record TableColumn(string TableName, string ColumnName);
}
