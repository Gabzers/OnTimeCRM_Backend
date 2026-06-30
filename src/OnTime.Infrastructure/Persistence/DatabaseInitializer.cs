using Microsoft.EntityFrameworkCore;

namespace OnTime.Infrastructure.Persistence;

/// <summary>
/// Applies EnsureCreated at application startup. All query/aggregate logic lives in C#/EF Core
/// now — see 04-DECISIONS/2026-05-30-data-layer.md for why the project moved away from raw
/// PostgreSQL `fn_*` functions (no SQL function application step here anymore).
///
/// Schema auto-recovery: if the DB already exists but is missing tables
/// (e.g. new entities added after the DB was first created), the DB is
/// dropped and recreated from scratch with the current model.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db, CancellationToken ct = default)
    {
        bool freshlyCreated = await db.Database.EnsureCreatedAsync(ct);

        if (!freshlyCreated)
        {
            // DB already existed — check for schema drift (42P01 auto-recovery)
            bool schemaCurrent = await IsSchemaCurrentAsync(db, ct);
            if (!schemaCurrent)
            {
                // Missing tables detected (new entities added since last deploy).
                // Drop and recreate — safe for the EnsureCreated / no-migration approach.
                await db.Database.EnsureDeletedAsync(ct);
                await db.Database.EnsureCreatedAsync(ct);
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
    private static async Task<bool> IsSchemaCurrentAsync(AppDbContext db, CancellationToken ct)
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

        if (!expectedTables.All(t => existingTableSet.Contains(t))) return false;

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
                if (!existingColumnSet.Contains(key)) return false;
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
                if (!existingIndexNames.Contains(indexName)) return false;
            }
        }

        return true;
    }

    private record TableColumn(string TableName, string ColumnName);
}
