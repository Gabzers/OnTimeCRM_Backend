using Microsoft.EntityFrameworkCore;
using OnTimeCRM.Infrastructure.Persistence.Sql;

namespace OnTimeCRM.Infrastructure.Persistence;

/// <summary>
/// Applies EnsureCreated and then creates/replaces all PostgreSQL functions
/// at application startup.  Safe to call on every startup — all statements
/// use CREATE OR REPLACE.
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

        // Drop all existing fn_* functions first so CREATE OR REPLACE can safely
        // change return types (PostgreSQL 42P13 would block otherwise).
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ DECLARE r RECORD;
            BEGIN
                FOR r IN
                    SELECT p.proname, pg_get_function_identity_arguments(p.oid) AS argtypes
                    FROM pg_proc p
                    JOIN pg_namespace n ON n.oid = p.pronamespace
                    WHERE n.nspname = 'public' AND p.prokind = 'f' AND p.proname LIKE 'fn_%'
                LOOP
                    EXECUTE 'DROP FUNCTION IF EXISTS public.' || quote_ident(r.proname) || '(' || r.argtypes || ') CASCADE';
                END LOOP;
            END $$;
            """, ct);

        // Apply all PostgreSQL functions
        foreach (var sql in DatabaseFunctions.All)
            await db.Database.ExecuteSqlRawAsync(sql, ct);
    }

    // ── Compares EF model entity tables against information_schema ────────
    private static async Task<bool> IsSchemaCurrentAsync(AppDbContext db, CancellationToken ct)
    {
        var existing = await db.Database
            .SqlQuery<string>($"SELECT table_name::text FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync(ct);

        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool allTablesExist = db.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(t => t is not null)
            .All(t => existingSet.Contains(t!));

        if (!allTablesExist) return false;

        // Also check for new columns added after the DB was first created.
        // If any sentinel new column is missing, the schema is stale — drop+recreate.
        var newClientColCount = await db.Database
            .SqlQuery<int>($"SELECT COUNT(1)::int AS \"Value\" FROM information_schema.columns WHERE table_name='notification_preferences' AND column_name='new_client_notification_days_after'")
            .FirstOrDefaultAsync(ct);

        return newClientColCount > 0;
    }
}
