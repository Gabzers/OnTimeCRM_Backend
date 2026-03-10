using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnTimeCRM.Application.Interfaces;
using OnTimeCRM.Application.Services;

namespace OnTimeCRM.Application.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IProposalService, ProposalService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IClientStageService, ClientStageService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
        services.AddScoped<IFriendshipService, FriendshipService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IUserGoalService, UserGoalService>();
        services.AddScoped<IPermissionService, PermissionService>();

        return services;
    }
}
