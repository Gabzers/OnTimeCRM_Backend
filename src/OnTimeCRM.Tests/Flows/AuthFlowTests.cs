using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using OnTimeCRM.Application.DTOs.Auth;
using OnTimeCRM.Domain.Enums;
using OnTimeCRM.Tests.Infrastructure;

namespace OnTimeCRM.Tests.Flows;

/// <summary>
/// Flow 1 — Authentication and Onboarding
/// Goal: Registration, login, JWT claims, and initial seed data are correct.
/// </summary>
[Collection("Integration")]
public class AuthFlowTests : IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public AuthFlowTests(TestWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Test 1 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterManager_CreatesCompanyBrandUserAndSeeds7Stages()
    {
        // ARRANGE
        var req = new RegisterManagerRequest(
            FullName: "João Silva",
            Email: "joao@grupoteste.pt",
            Password: "Teste123!",
            Phone: "351961234567",
            CompanyName: "Grupo Teste Lda",
            BrandName: "Stand Toyota Lisboa",
            BrandColor: "#1677FF"
        );

        // ACT
        var response = await _factory.Client.PostAsJsonAsync("/api/auth/register-manager", req);

        // ASSERT — HTTP 200 + token
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        result.ShouldNotBeNull();
        result!.Token.ShouldNotBeNullOrEmpty();
        result.AccountStatus.ShouldBe((int)UserAccountStatus.PendingActivation);
        result.SubscriptionStatus.ShouldBe((int)SubscriptionStatus.Trial);
        result.CompanyId.ShouldNotBe(Guid.Empty);
        result.BrandId.ShouldNotBe(Guid.Empty);
        result.UserId.ShouldNotBe(Guid.Empty);

        // ASSERT — Company created
        var company = await _factory.Db.Companies.FindAsync(result.CompanyId);
        company.ShouldNotBeNull();
        company!.Name.ShouldBe("Grupo Teste Lda");
        company.IsActive.ShouldBeTrue();

        // ASSERT — Brand created and linked to company
        var brand = await _factory.Db.Brands.FindAsync(result.BrandId);
        brand.ShouldNotBeNull();
        brand!.Name.ShouldBe("Stand Toyota Lisboa");
        brand.CompanyId.ShouldBe(result.CompanyId!.Value);

        // ASSERT — User created with Manager role
        var user = await _factory.Db.Users.FindAsync(result.UserId);
        user.ShouldNotBeNull();
        user!.Role.ShouldBe(UserRole.Manager);
        user.AccountStatus.ShouldBe(UserAccountStatus.PendingActivation);
        user.TrialEndsAt.ShouldNotBeNull();

        // ASSERT — 7 default stages seeded for user
        var stages = await _factory.Db.ClientStages
            .Where(s => s.UserId == result.UserId)
            .OrderBy(s => s.Order)
            .ToListAsync();
        stages.Count.ShouldBe(7);
        stages[0].Name.ShouldBe("Aguarda Agendamento de Visita");
        stages[5].IsWon.ShouldBeTrue();
        stages[6].IsLost.ShouldBeTrue();

        // ASSERT — 3 notification templates seeded (stages 1, 4, 5)
        var templates = await _factory.Db.StageNotificationTemplates
            .Where(t => t.UserId == result.UserId)
            .ToListAsync();
        templates.Count.ShouldBe(3);

        // ASSERT — Notification preferences created with defaults
        var prefs = await _factory.Db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == result.UserId);
        prefs.ShouldNotBeNull();
        prefs!.DailyDigestTime.ShouldBe(new TimeOnly(9, 29));
        prefs.SaleFollowUpDays.ShouldBe(30);
        prefs.DigestEnabled.ShouldBeTrue();
    }

    // ── Test 2 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsTokenWithCompanyAndBrandClaims()
    {
        // ARRANGE — register first
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client, "login_test@test.pt");

        // ACT — login again
        var loginReq = new LoginRequest(Email: "login_test@test.pt", Password: "Teste123!");
        var response = await _factory.Client.PostAsJsonAsync("/api/auth/login", loginReq);

        // ASSERT
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        result.ShouldNotBeNull();
        result!.Token.ShouldNotBeNullOrEmpty();
        result.CompanyId.ShouldBe(auth.CompanyId);
        result.BrandId.ShouldBe(auth.BrandId);
        result.UserId.ShouldBe(auth.UserId);
        result.Role.ShouldBe((int)UserRole.Manager);
    }

    // ── Test 3 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithWrongPassword_Returns401WithStructuredError()
    {
        // ARRANGE
        await TestHelpers.RegisterManagerAsync(_factory.Client, "wrongpwd@test.pt");

        // ACT
        var loginReq = new LoginRequest(Email: "wrongpwd@test.pt", Password: "WrongPassword!");
        var response = await _factory.Client.PostAsJsonAsync("/api/auth/login", loginReq);

        // ASSERT
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("USER_INVALID_CREDENTIALS");
    }

    // ── Test 4 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithInactiveUser_Returns401UserInactive()
    {
        // ARRANGE — register and then deactivate
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client, "inactive@test.pt");
        var user = await _factory.Db.Users.FindAsync(auth.UserId);
        user!.IsActive = false;
        await _factory.Db.SaveChangesAsync();
        _factory.Db.ChangeTracker.Clear();

        // ACT
        var loginReq = new LoginRequest(Email: "inactive@test.pt", Password: "Teste123!");
        var response = await _factory.Client.PostAsJsonAsync("/api/auth/login", loginReq);

        // ASSERT
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("USER_INACTIVE");
    }

    // ── Test 5 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterSalesperson_WithValidBrandId_CreatesUserScopedToBrand()
    {
        // ARRANGE — register manager first to get company/brand IDs
        var managerAuth = await TestHelpers.RegisterManagerAsync(_factory.Client);

        // ACT
        var spAuth = await TestHelpers.RegisterSalespersonAsync(
            _factory.Client, managerAuth.CompanyId, managerAuth.BrandId, "salesperson@test.pt");

        // ASSERT
        spAuth.CompanyId.ShouldBe(managerAuth.CompanyId);
        spAuth.BrandId.ShouldBe(managerAuth.BrandId);

        var user = await _factory.Db.Users.FindAsync(spAuth.UserId);
        user.ShouldNotBeNull();
        user!.Role.ShouldBe(UserRole.Salesperson);
        user.BrandId.ShouldBe(managerAuth.BrandId);
        user.CompanyId.ShouldBe(managerAuth.CompanyId);
    }

    // ── Test 6 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DuplicateEmail_Returns409WithUserEmailTakenCode()
    {
        // ARRANGE — register once
        const string email = "duplicate@test.pt";
        await TestHelpers.RegisterManagerAsync(_factory.Client, email);

        // ACT — register again with the same email
        var req = new RegisterManagerRequest(
            FullName: "Second Person",
            Email: email,
            Password: "Teste123!",
            Phone: null,
            CompanyName: "Second Company",
            BrandName: "Second Brand",
            BrandColor: null
        );
        var response = await _factory.Client.PostAsJsonAsync("/api/auth/register-manager", req);

        // ASSERT
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("USER_EMAIL_TAKEN");
    }
}

[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<TestWebAppFactory> { }
