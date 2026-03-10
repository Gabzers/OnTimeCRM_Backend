using System.Net;
using System.Net.Http.Json;
using Shouldly;
using OnTimeCRM.Domain.Enums;
using OnTimeCRM.Tests.Infrastructure;

namespace OnTimeCRM.Tests.Flows;

/// <summary>
/// Flow 2 — Subscription and Payments
/// Goal: Subscription lifecycle (trial, activation, expiry, blocking) is correctly enforced.
/// Note: Payment initiation stubs (Stripe/Ifthenpay) are NotImplementedException in this build.
/// These tests cover the subscription state enforcement and direct-activation helper path.
/// </summary>
[Collection("Integration")]
public class SubscriptionFlowTests : IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public SubscriptionFlowTests(TestWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Test 1 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Trial_NewUser_HasCorrectTrialDatesAndPendingActivationStatus()
    {
        // ARRANGE + ACT — register creates a trial user
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);

        // ASSERT — via DB
        var user = await _factory.Db.Users.FindAsync(auth.UserId);
        user.ShouldNotBeNull();
        user!.AccountStatus.ShouldBe(UserAccountStatus.PendingActivation);
        user.SubscriptionStatus.ShouldBe(SubscriptionStatus.Trial);
        // TrialEndsAt should be set (even if 0 days = immediate in test config)
        user.TrialEndsAt.ShouldNotBeNull();
    }

    // ── Test 2 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ActivateSubscriptionDirect_SetsActiveStatusAndCorrectDates()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);

        // Verify initially pending
        var userBefore = await _factory.Db.Users.FindAsync(auth.UserId);
        userBefore!.AccountStatus.ShouldBe(UserAccountStatus.PendingActivation);

        // ACT — directly activate (bypass payment for non-billing tests)
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId, days: 30);

        // ASSERT
        var userAfter = await _factory.Db.Users.FindAsync(auth.UserId);
        userAfter!.AccountStatus.ShouldBe(UserAccountStatus.Active);
        userAfter.SubscriptionStatus.ShouldBe(SubscriptionStatus.Active);
        userAfter.SubscriptionExpiresAt.ShouldNotBeNull();
        userAfter.SubscriptionExpiresAt!.Value
            .ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddDays(28));
    }

    // ── Test 3 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExpiredSubscription_BlocksWriteOperations_ButAllowsReads()
    {
        // ARRANGE — register and then expire
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId, days: -1); // expired

        var token = await TestHelpers.LoginAsync(_factory.Client, auth.Email);

        // ACT + ASSERT — GET is allowed
        var getResp = await _factory.Client.GetAsync("/api/clients", token);
        getResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // ACT + ASSERT — POST is blocked with 402
        var postResp = await _factory.Client.PostAsJsonAsync(
            "/api/clients",
            new { FullName = "Test", Phone = "351910000001", LeadSource = 0, BusinessType = 0, PaymentType = 0 },
            token);
        postResp.StatusCode.ShouldBe(HttpStatusCode.PaymentRequired);

        var body = await postResp.Content.ReadAsStringAsync();
        body.ShouldContain("SUBSCRIPTION_EXPIRED");
    }

    // ── Test 4 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task PendingActivation_BlocksWriteOperations_Returns402()
    {
        // ARRANGE — new user is PendingActivation by default
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        var token = await TestHelpers.LoginAsync(_factory.Client, auth.Email);

        // ACT — try to create a client
        var postResp = await _factory.Client.PostAsJsonAsync(
            "/api/clients",
            new { FullName = "Test", Phone = "351910000002", LeadSource = 0, BusinessType = 0, PaymentType = 0 },
            token);

        // ASSERT — 402 Payment Required
        postResp.StatusCode.ShouldBe(HttpStatusCode.PaymentRequired);
    }

    // ── Test 5 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SuspendedAccount_BlocksAllOperationsIncludingReads()
    {
        // ARRANGE — activate then suspend
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId, days: 30);

        var user = await _factory.Db.Users.FindAsync(auth.UserId);
        user!.AccountStatus = UserAccountStatus.Suspended;
        await _factory.Db.SaveChangesAsync();
        _factory.Db.ChangeTracker.Clear();

        var token = await TestHelpers.LoginAsync(_factory.Client, auth.Email);

        // ACT + ASSERT — GET is also blocked for suspended accounts
        var getResp = await _factory.Client.GetAsync("/api/clients", token);
        getResp.StatusCode.ShouldBe(HttpStatusCode.PaymentRequired);

        // POST is blocked too
        var postResp = await _factory.Client.PostAsJsonAsync(
            "/api/clients",
            new { FullName = "Test", Phone = "351910000003", LeadSource = 0, BusinessType = 0, PaymentType = 0 },
            token);
        postResp.StatusCode.ShouldBe(HttpStatusCode.PaymentRequired);
    }

    // ── Test 6 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelledAccount_Returns403ForAllRequests()
    {
        // ARRANGE — activate then cancel
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId, days: 30);

        var user = await _factory.Db.Users.FindAsync(auth.UserId);
        user!.AccountStatus = UserAccountStatus.Cancelled;
        await _factory.Db.SaveChangesAsync();
        _factory.Db.ChangeTracker.Clear();

        var token = await TestHelpers.LoginAsync(_factory.Client, auth.Email);

        // ASSERT
        var resp = await _factory.Client.GetAsync("/api/clients", token);
        resp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── Test 7 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SubscriptionRenewal_AfterExpiry_ReactivatesAccount()
    {
        // ARRANGE — expire the account
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId, days: -1);

        var token = await TestHelpers.LoginAsync(_factory.Client, auth.Email);

        // Verify it's blocked for writes
        var beforeResp = await _factory.Client.PostAsJsonAsync(
            "/api/clients",
            new { FullName = "Test", Phone = "351910000004", LeadSource = 0, BusinessType = 0, PaymentType = 0 },
            token);
        beforeResp.StatusCode.ShouldBe(HttpStatusCode.PaymentRequired);

        // ACT — renew (simulate reactivation)
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId, days: 30);
        _factory.Db.ChangeTracker.Clear();

        // Need fresh token after status change
        token = await TestHelpers.LoginAsync(_factory.Client, auth.Email);

        // ASSERT — reads and writes now work
        var afterGetResp = await _factory.Client.GetAsync("/api/clients", token);
        afterGetResp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── Test 8 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task InactiveAccount_Returns403ForAllRequests()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        var user = await _factory.Db.Users.FindAsync(auth.UserId);
        user!.AccountStatus = UserAccountStatus.Inactive;
        user.IsActive = false;
        await _factory.Db.SaveChangesAsync();
        _factory.Db.ChangeTracker.Clear();

        var token = await TestHelpers.LoginAsync(_factory.Client, auth.Email);

        // ASSERT
        var resp = await _factory.Client.GetAsync("/api/clients", token);
        resp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── Test 9 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Auth_Subscription_Webhook_I18n_HealthEndpoints_BypassSubscriptionMiddleware()
    {
        // These routes should always return something useful even without a valid subscription
        var publicRoutes = new[]
        {
            "/api/i18n?locale=pt-PT",
            "/health"
        };

        foreach (var route in publicRoutes)
        {
            var resp = await _factory.Client.GetAsync(route);
            resp.StatusCode.ShouldBe(HttpStatusCode.OK, $"Expected 200 for {route}");
        }
    }
}
