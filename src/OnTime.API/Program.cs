using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OnTime.Application.Extensions;
using OnTime.Application.Interfaces;
using OnTime.Domain.Entities;
using OnTime.Domain.Enums;
using OnTime.Infrastructure.Extensions;
using OnTime.Infrastructure.Persistence;
using OnTime.API.Middleware;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        opt.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is required.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    // Manager (1) or Admin (2) can access manager-only endpoints
    options.AddPolicy("ManagerOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim(System.Security.Claims.ClaimTypes.Role, "1", "2"));

    // Platform Admin (2) only — cross-tenant endpoints (e.g. the admin panel that lists/
    // disables/edits ALL companies) must never be reachable by a regular Manager (1), who
    // is a customer of the SaaS, not its operator.
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim(System.Security.Claims.ClaimTypes.Role, "2"));
});

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins);
        else
            policy.AllowAnyOrigin();

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "OnTime API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In   = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Id = "Bearer", Type = ReferenceType.SecurityScheme } },
            Array.Empty<string>()
        }
    });
});

// Health checks
builder.Services.AddHealthChecks();

// Rate limiting — login is the brute-force target. Partitioned by client IP (not a single
// shared counter) so one attacker can't lock out every other user; queueing is disabled so
// excess requests fail fast with 429 instead of piling up.
// Limit is configurable so the integration test suite (many sequential logins through one
// shared TestServer/IP) can raise it instead of tripping on itself — see appsettings used by
// TestWebAppFactory, which sets this very high.
var loginPermitPerMinute = builder.Configuration.GetValue<int?>("RateLimiting:LoginPermitPerMinute") ?? 5;
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginPermitPerMinute,
                Window      = TimeSpan.FromMinutes(1),
                QueueLimit  = 0,
            }));
    options.OnRejected = (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        return new ValueTask();
    };
});

// ── App pipeline ──────────────────────────────────────────────────────────────
var app = builder.Build();

// EnsureCreated + Seed at startup
await InitializeDatabaseAsync(app);

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OnTime v1"));
app.MapScalarApiReference();

app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<SubscriptionAccessMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// ── Startup initialization ────────────────────────────────────────────────────
static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db      = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var config  = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var hasher  = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    var logger  = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await DatabaseInitializer.InitializeAsync(db, app.Environment.IsProduction());

        // Seed global vehicle brands (shared across all tenants)
        await SeedVehicleBrandsAsync(db);

        // AdminBootstrap: create demo company/brand/admin only if no companies exist
        await AdminBootstrapAsync(db, config, hasher, logger);

        await db.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization failed");
        throw;
    }
}

static async Task SeedVehicleBrandsAsync(AppDbContext db)
{
    // Per-brand idempotent: a brand is seeded only if it doesn't exist yet, so brands
    // added here also appear on an already-seeded local DB (no full reset needed).
    var catalogue = new Dictionary<string, string[]>
    {
        ["Dongfeng"] = ["AX7", "AX4", "AX3", "T5 EVO", "ix5", "580 Pro",
                        "AEOLUS E70", "Forthing 580 Pro", "Forthing T5 EVO",
                        "Mengshi 917", "Mengshi 919"],
        ["Voyah"]    = ["Free", "Dream", "Passion", "Range-E"],
        ["XPENG"]    = ["P7", "P7+", "P5", "MONA M03", "G3i", "G6", "G7", "G9", "X9"],
    };

    foreach (var (brandName, models) in catalogue)
    {
        if (await db.VehicleBrands.AnyAsync(b => b.Name == brandName)) continue;

        var brand = new VehicleBrand { Name = brandName };
        db.VehicleBrands.Add(brand);
        foreach (var m in models)
            db.VehicleModels.Add(new VehicleModel { Brand = brand, Name = m });
    }
}

static async Task AdminBootstrapAsync(
    AppDbContext db,
    IConfiguration config,
    IPasswordHasher hasher,
    ILogger<Program> logger)
{
    if (await db.Companies.AnyAsync()) return;

    var email    = config["AdminBootstrap:Email"];
    var password = config["AdminBootstrap:Password"];
    var fullName = config["AdminBootstrap:FullName"] ?? "Administrador";
    var company  = config["AdminBootstrap:CompanyName"] ?? "OnTime Demo";
    var brand    = config["AdminBootstrap:BrandName"]   ?? "Stand Demo";

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        logger.LogWarning("AdminBootstrap skipped: Email or Password not configured.");
        return;
    }

    var companyEntity = new Company { Name = company, IsActive = true };
    db.Companies.Add(companyEntity);

    var brandEntity = new Brand { Company = companyEntity, Name = brand, PrimaryColor = "#1677FF", IsActive = true };
    db.Brands.Add(brandEntity);

    var adminUser = new User
    {
        Company       = companyEntity,
        Brand         = brandEntity,
        Email         = email.ToLower(),
        PasswordHash  = hasher.Hash(password),
        FullName      = fullName,
        Role          = UserRole.Admin,
        AccountStatus = UserAccountStatus.Active,
        IsActive      = true
    };
    db.Users.Add(adminUser);

    SeedAdminDefaults(db, adminUser);

    logger.LogInformation("AdminBootstrap: Created admin user {Email}", email);
}

static void SeedAdminDefaults(AppDbContext db, User user)
{
    var stages = new[]
    {
        new ClientStage { User = user, Name = "Aguarda Agendamento de Visita", Color = "#94A3B8", Order = 0 },
        new ClientStage { User = user, Name = "Visita Agendada",               Color = "#3B82F6", Order = 1 },
        new ClientStage { User = user, Name = "Agendar Test Drive",             Color = "#8B5CF6", Order = 2 },
        new ClientStage { User = user, Name = "Test Drive Marcado",             Color = "#F59E0B", Order = 3 },
        new ClientStage { User = user, Name = "Aguarda Decisao",               Color = "#EF4444", Order = 4 },
        new ClientStage { User = user, Name = "Venda",   Color = "#10B981", Order = 5, IsFinal = true, IsWon  = true  },
        new ClientStage { User = user, Name = "Perdido", Color = "#6B7280", Order = 6, IsFinal = true, IsLost = true },
    };

    foreach (var s in stages) db.ClientStages.Add(s);

    db.StageNotificationTemplates.Add(new StageNotificationTemplate { Stage = stages[1], Title = "Confirmar visita",   DaysAfter = 1  });
    db.StageNotificationTemplates.Add(new StageNotificationTemplate { Stage = stages[4], Title = "Ligar ao cliente",   DaysAfter = 2  });
    db.StageNotificationTemplates.Add(new StageNotificationTemplate { Stage = stages[5], Title = "Contacto pos-venda", DaysAfter = 30 });

    db.NotificationPreferences.Add(new NotificationPreference
    {
        User                            = user,
        DailyDigestTime                 = new TimeOnly(9, 29),
        DigestFrequencyDays             = 2,
        SaleFollowUpDays                = 30,
        DigestEnabled                   = true,
        StageChangeNotificationsEnabled = true,
        SaleNotificationsEnabled        = true
    });

    var leadSourceNames = new[] { "Stand", "Telefone", "Instagram", "Facebook", "Recomendação", "Outro" };
    for (var code = 0; code < leadSourceNames.Length; code++)
        db.LeadSourceOptions.Add(new LeadSourceOption { User = user, Code = code, Name = leadSourceNames[code] });
}

// Make Program partial for test accessibility
public partial class Program { }

