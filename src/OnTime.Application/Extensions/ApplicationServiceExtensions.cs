using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnTime.Application.Interfaces;
using OnTime.Application.Services;

namespace OnTime.Application.Extensions;

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
        services.AddScoped<IErrorLogService, ErrorLogService>();
        services.AddScoped<IUserGoalService, UserGoalService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ILeadSourceService, LeadSourceService>();
        services.AddScoped<IScheduledJobsService, ScheduledJobsService>();

        return services;
    }
}
