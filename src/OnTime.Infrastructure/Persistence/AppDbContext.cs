using Microsoft.EntityFrameworkCore;
using OnTime.Application.Interfaces;
using OnTime.Domain.Common;
using OnTime.Domain.Entities;

namespace OnTime.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSubscriptionPayment> UserSubscriptionPayments => Set<UserSubscriptionPayment>();
    public DbSet<ClientStage> ClientStages => Set<ClientStage>();
    public DbSet<StageNotificationTemplate> StageNotificationTemplates => Set<StageNotificationTemplate>();
    public DbSet<ClientStageTemperatureRule> ClientStageTemperatureRules => Set<ClientStageTemperatureRule>();
    public DbSet<ClientStageNotificationSeries> ClientStageNotificationSeries => Set<ClientStageNotificationSeries>();
    public DbSet<VehicleBrand> VehicleBrands => Set<VehicleBrand>();
    public DbSet<VehicleModel> VehicleModels => Set<VehicleModel>();
    public DbSet<VehicleModelVersion> VehicleModelVersions => Set<VehicleModelVersion>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientStageHistory> ClientStageHistories => Set<ClientStageHistory>();
    public DbSet<Proposal> Proposals => Set<Proposal>();
    public DbSet<ProposalVehicle> ProposalVehicles => Set<ProposalVehicle>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<TranslationEntry> TranslationEntries => Set<TranslationEntry>();
    public DbSet<UserFriendship> UserFriendships => Set<UserFriendship>();
    public DbSet<UserPublicProfile> UserPublicProfiles => Set<UserPublicProfile>();
    public DbSet<UserGoal> UserGoals => Set<UserGoal>();
    public DbSet<MenuItemPermission> MenuItemPermissions => Set<MenuItemPermission>();
    public DbSet<UserVehicleModel> UserVehicleModels => Set<UserVehicleModel>();
    public DbSet<UserVehicleVersion> UserVehicleVersions => Set<UserVehicleVersion>();
    public DbSet<UserBrandMembership> UserBrandMemberships => Set<UserBrandMembership>();
    public DbSet<BrandVehicleBrand> BrandVehicleBrands => Set<BrandVehicleBrand>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
    public DbSet<LeadSourceOption> LeadSourceOptions => Set<LeadSourceOption>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
