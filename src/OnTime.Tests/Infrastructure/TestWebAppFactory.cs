using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using WireMock.Server;
using OnTime.Domain.Entities;
using OnTime.Infrastructure.Persistence;

namespace OnTime.Tests.Infrastructure;

/// <summary>
/// Boots the full ASP.NET Core API in-process with a real Postgres container (via Testcontainers)
/// and a WireMock server for external APIs (Stripe, Ifthenpay).
/// One instance is shared per test collection; each test class resets the DB via ResetDatabaseAsync().
/// </summary>
public class TestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer _postgres = default!;
    private WireMockServer _wireMock = default!;
    private Respawner _respawner = default!;
    private NpgsqlConnection _respawnConnection = default!;

    // Exposed so test classes can make direct DB assertions
    public AppDbContext Db { get; private set; } = default!;

    // Shared HTTP client (call CreateClient() only once per factory lifetime)
    public HttpClient Client { get; private set; } = default!;

    public string WireMockBaseUrl => _wireMock.Url!;

    async Task IAsyncLifetime.InitializeAsync()
    {
        // 1. Start real Postgres via Docker
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("ontimecrm_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _postgres.StartAsync();

        // 2. Start WireMock for external API mocks
        _wireMock = WireMockServer.Start();
        ExternalApiMocks.SetupStripeMocks(_wireMock);
        ExternalApiMocks.SetupIfthenpayMocks(_wireMock);

        // 3. Create the in-process HTTP client (triggers WebApplicationFactory configuration)
        Client = CreateClient();

        // 4. Resolve a long-lived DbContext for direct DB assertions
        var scope = Services.CreateScope();
        Db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseInitializer.InitializeAsync(Db);

        // 5. Configure Respawn (cleans data between tests without dropping schema)
        _respawnConnection = new NpgsqlConnection(_postgres.GetConnectionString());
        await _respawnConnection.OpenAsync();

        _respawner = await Respawner.CreateAsync(_respawnConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Respawn.Graph.Table("vehicle_brands")] // global seed data
        });
    }

    /// <summary>
    /// Called by each test class in its InitializeAsync to get a clean DB slate for every test run.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await _respawner.ResetAsync(_respawnConnection);

        // vehicle_brands is excluded from Respawn (global seed data), but vehicle_models is not,
        // so it gets wiped every reset — reseed brands+models the first time, models-only after.
        var dongfeng = await Db.VehicleBrands.FirstOrDefaultAsync(b => b.Name == "Dongfeng");
        var voyah = await Db.VehicleBrands.FirstOrDefaultAsync(b => b.Name == "Voyah");
        if (dongfeng is null || voyah is null)
        {
            await SeedVehicleBrandsAsync();
        }
        else if (!await Db.VehicleModels.AnyAsync())
        {
            await SeedVehicleModelsAsync(dongfeng, voyah);
        }

        // Clear the DbContext's local cache so it re-reads from DB
        Db.ChangeTracker.Clear();
    }

    private async Task SeedVehicleBrandsAsync()
    {
        var dongfeng = new VehicleBrand { Name = "Dongfeng" };
        Db.VehicleBrands.Add(dongfeng);

        var voyah = new VehicleBrand { Name = "Voyah" };
        Db.VehicleBrands.Add(voyah);

        await SeedVehicleModelsAsync(dongfeng, voyah);
    }

    private async Task SeedVehicleModelsAsync(VehicleBrand dongfeng, VehicleBrand voyah)
    {
        foreach (var m in new[] { "AX7", "AX4", "AX3", "T5 EVO", "Free Line" })
            Db.VehicleModels.Add(new VehicleModel { Brand = dongfeng, Name = m });

        foreach (var m in new[] { "Free", "Dream", "Passion", "Range-E" })
            Db.VehicleModels.Add(new VehicleModel { Brand = voyah, Name = m });

        await Db.SaveChangesAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace the real DbContext with one pointing to the test Postgres container
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(options =>
                options
                    .UseNpgsql(_postgres.GetConnectionString())
                    .UseSnakeCaseNamingConvention());

            services.AddScoped<OnTime.Application.Interfaces.IAppDbContext>(
                sp => sp.GetRequiredService<AppDbContext>());
        });

        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres?.GetConnectionString() ?? "");
        builder.UseSetting("Jwt:Key", "test-super-secret-key-for-tests-at-least-32");
        builder.UseSetting("Jwt:Issuer", "OnTime");
        builder.UseSetting("Jwt:Audience", "OnTime-Frontend");
        builder.UseSetting("AdminBootstrap:Email", "");  // skip admin bootstrap in tests
        builder.UseSetting("AdminBootstrap:Password", "");
        builder.UseSetting("Subscription:TrialDays", "0");
        builder.UseSetting("Subscription:GracePeriodDays", "0");
        builder.UseSetting("Stripe:BaseUrl", _wireMock?.Url ?? "");
        builder.UseSetting("Stripe:SecretKey", "sk_test_fake");
        builder.UseSetting("Stripe:WebhookSecret", "whsec_test_fake");
        builder.UseSetting("Ifthenpay:BaseUrl", _wireMock?.Url ?? "");
        builder.UseSetting("Ifthenpay:MBWayKey", "MBWAY_TEST_KEY");
        builder.UseSetting("Ifthenpay:MultibancoKey", "MB_TEST_KEY");
        builder.UseSetting("Ifthenpay:Entity", "11111");
        builder.UseSetting("Ifthenpay:CallbackSecretKey", "callback_test_secret");

        // The whole suite shares one TestServer/IP through this factory, so the production
        // login rate limit (5/min) would trip across unrelated tests. Raise it here instead of
        // disabling the limiter outright, so RateLimitingFlowTests can still verify the real
        // limit by passing its own low override per-request via a dedicated factory if needed.
        builder.UseSetting("RateLimiting:LoginPermitPerMinute", "100000");
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _respawnConnection.DisposeAsync();
        await Db.DisposeAsync();
        Client.Dispose();
        await _postgres.DisposeAsync();
        _wireMock.Stop();
        _wireMock.Dispose();
    }
}
