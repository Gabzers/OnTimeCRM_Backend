using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnTimeCRM.Application.Interfaces;
using OnTimeCRM.Application.Interfaces.Repositories;
using OnTimeCRM.Infrastructure.Persistence;
using OnTimeCRM.Infrastructure.Repositories;
using OnTimeCRM.Infrastructure.Security;

namespace OnTimeCRM.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options
                .UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IAuthRepository,                   AuthRepository>();
        services.AddScoped<IBrandRepository,                  BrandRepository>();
        services.AddScoped<IClientRepository,                 ClientRepository>();
        services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();
        services.AddScoped<INotificationRepository,           NotificationRepository>();
        services.AddScoped<IProposalRepository,               ProposalRepository>();
        services.AddScoped<ISaleRepository,                   SaleRepository>();
        services.AddScoped<IStageRepository,                  StageRepository>();
        services.AddScoped<IUserRepository,                   UserRepository>();
        services.AddScoped<IVehicleRepository,                VehicleRepository>();
        services.AddScoped<IFriendshipRepository,             FriendshipRepository>();
        services.AddScoped<IAdminRepository,                  AdminRepository>();

        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IJwtService, JwtService>();

        return services;
    }
}
