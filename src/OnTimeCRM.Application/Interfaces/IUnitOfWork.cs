namespace OnTimeCRM.Application.Interfaces;

/// <summary>
/// Abstracts the database commit so services stay decoupled from EF Core.
/// AppDbContext implements this; all scoped repositories share the same context instance,
/// so one SaveChangesAsync call commits everything added/modified across all repos.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
