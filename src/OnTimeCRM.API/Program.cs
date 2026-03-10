using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OnTimeCRM.Application.Extensions;
using OnTimeCRM.Application.Interfaces;
using OnTimeCRM.Domain.Entities;
using OnTimeCRM.Domain.Enums;
using OnTimeCRM.Infrastructure.Extensions;
using OnTimeCRM.Infrastructure.Persistence;
using OnTimeCRM.API.Middleware;
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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "OnTimeCRM API", Version = "v1" });
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

// ── App pipeline ──────────────────────────────────────────────────────────────
var app = builder.Build();

// EnsureCreated + Seed at startup
await InitializeDatabaseAsync(app);

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OnTimeCRM v1"));
app.MapScalarApiReference();

app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseCors();
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
        await DatabaseInitializer.InitializeAsync(db);

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
    if (await db.VehicleBrands.AnyAsync()) return;

    // ── Dongfeng ──────────────────────────────────────────────────────────
    var dongfeng = new VehicleBrand { Name = "Dongfeng" };
    db.VehicleBrands.Add(dongfeng);
    var dongfengModels = new[]
    {
        "AX7", "AX4", "AX3", "T5 EVO", "ix5", "580 Pro",
        "AEOLUS E70", "Forthing 580 Pro", "Forthing T5 EVO",
        "Mengshi 917", "Mengshi 919"
    };
    foreach (var m in dongfengModels)
        db.VehicleModels.Add(new VehicleModel { Brand = dongfeng, Name = m });

    // ── Voyah ─────────────────────────────────────────────────────────────
    var voyah = new VehicleBrand { Name = "Voyah" };
    db.VehicleBrands.Add(voyah);
    var voyahModels = new[]
    {
        "Free", "Dream", "Passion", "Range-E"
    };
    foreach (var m in voyahModels)
        db.VehicleModels.Add(new VehicleModel { Brand = voyah, Name = m });
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
    var company  = config["AdminBootstrap:CompanyName"] ?? "OnTimeCRM Demo";
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
}

// Make Program partial for test accessibility
public partial class Program { }

