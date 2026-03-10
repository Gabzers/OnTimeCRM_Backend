using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Application.Interfaces;

/// <summary>
/// Abstraction over AppDbContext so Application services never reference the Infrastructure project.
/// Infrastructure implements this; Application only knows the interface.
/// </summary>
public interface IAppDbContext
{
    DbSet<Company> Companies { get; }
    DbSet<Brand> Brands { get; }
    DbSet<User> Users { get; }
    DbSet<UserSubscriptionPayment> UserSubscriptionPayments { get; }
    DbSet<ClientStage> ClientStages { get; }
    DbSet<StageNotificationTemplate> StageNotificationTemplates { get; }
    DbSet<VehicleBrand> VehicleBrands { get; }
    DbSet<VehicleModel> VehicleModels { get; }
    DbSet<Client> Clients { get; }
    DbSet<ClientStageHistory> ClientStageHistories { get; }
    DbSet<Proposal> Proposals { get; }
    DbSet<ProposalVehicle> ProposalVehicles { get; }
    DbSet<Sale> Sales { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }
    DbSet<TranslationEntry> TranslationEntries { get; }
    DbSet<UserGoal> UserGoals { get; }
    DbSet<MenuItemPermission> MenuItemPermissions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    DatabaseFacade Database { get; }
}
