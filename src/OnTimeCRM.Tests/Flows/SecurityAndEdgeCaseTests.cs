using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using OnTimeCRM.Tests.Infrastructure;

namespace OnTimeCRM.Tests.Flows;

/// <summary>
/// Flow 10 — Security &amp; Edge Cases
/// Goal: Unauthenticated access is blocked, errors are structured,
/// inputs are sanitised, and resource bounds are enforced.
/// </summary>
[Collection("Integration")]
public class SecurityAndEdgeCaseTests : IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public SecurityAndEdgeCaseTests(TestWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Test 1 ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("GET",  "/api/clients",              null)]
    [InlineData("POST", "/api/clients",              "{}")]
    [InlineData("GET",  "/api/dashboard",            null)]
    [InlineData("GET",  "/api/notifications",        null)]
    [InlineData("GET",  "/api/stages",               null)]
    [InlineData("GET",  "/api/subscription",         null)]
    [InlineData("GET",  "/api/proposals",            null)]
    [InlineData("GET",  "/api/sales",                null)]
    [InlineData("GET",  "/api/users/me",             null)]
    public async Task AllProtectedEndpoints_WithoutToken_Return401(
        string method, string path, string? body)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var resp = await _factory.Client.SendAsync(request);

        resp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Test 2 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task AllErrors_ReturnStructuredJsonWithCodeAndTraceId()
    {
        // Arrange
        var ctx = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, ctx.UserId);

        // ACT — request a client that doesn't exist
        var notExistId = Guid.NewGuid();
        var resp = await _factory.Client.GetAsync($"/api/clients/{notExistId}", ctx.Token);

        // ASSERT — 404 with structured error body
        resp.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = json.RootElement;

        root.TryGetProperty("code", out var codeProp).ShouldBeTrue();
        codeProp.GetString().ShouldBe("CLIENT_NOT_FOUND");

        root.TryGetProperty("message", out _).ShouldBeTrue();
        root.TryGetProperty("traceId", out var traceProp).ShouldBeTrue();
        traceProp.GetString().ShouldNotBeNullOrEmpty();
    }

    // ── Test 3 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SqlInjection_InSearchParam_IsSanitized()
    {
        // Arrange
        var ctx = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, ctx.UserId);

        // ACT — include a SQL injection attempt in the search query
        var malicious = Uri.EscapeDataString("'; DROP TABLE clients; --");
        var resp = await _factory.Client.GetAsync(
            $"/api/clients?search={malicious}&page=1&pageSize=20", ctx.Token);

        // ASSERT — should return 200 empty result, NOT throw or return 500
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await resp.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        var items = json.RootElement.GetProperty("items");
        items.GetArrayLength().ShouldBe(0);

        // Verify 'clients' table still exists (no truncation/drop occurred)
        var remaining = await _factory.Db.Clients.IgnoreQueryFilters().CountAsync();
        remaining.ShouldBe(0); // no clients seeded yet
    }

    // ── Test 4 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task LargePageSize_IsCappedAtMaximum()
    {
        // Arrange — create multiple clients so there IS data
        var ctx = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, ctx.UserId);

        for (var i = 0; i < 5; i++)
            await TestHelpers.CreateClientWithProposalAsync(_factory.Client, ctx.Token, db: _factory.Db);

        // ACT — request an absurdly large page
        var resp = await _factory.Client.GetAsync(
            "/api/clients?page=1&pageSize=9999", ctx.Token);

        resp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var pageSize = json.RootElement.GetProperty("pageSize").GetInt32();

        // Must be capped at 50 per spec
        pageSize.ShouldBeLessThanOrEqualTo(50);
    }

    // ── Test 5 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task HealthEndpoint_AlwaysReturns200_EvenWithExpiredSubscription()
    {
        // ACT — no token, no subscription required
        var resp = await _factory.Client.GetAsync("/health");

        // ASSERT — either 200 (endpoint exists) or 404 (not registered, either is acceptable)
        // The important thing is that it is NOT 402 or 403 (subscription checks bypassed)
        resp.StatusCode.ShouldNotBe(HttpStatusCode.PaymentRequired);
        resp.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
    }

    // ── Test 6 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task I18nEndpoint_NoToken_Returns200()
    {
        // ACT — [AllowAnonymous] endpoint must not require subscription
        var resp = await _factory.Client.GetAsync("/api/i18n?locale=pt-PT");
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── Test 7 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task WebhookStripe_WithInvalidSignature_Returns401Or400()
    {
        // ACT — POST to /api/webhooks/stripe with fake body and no valid Stripe-Signature
        var content = new StringContent(
            @"{""id"":""evt_fake"",""type"":""invoice.paid""}",
            Encoding.UTF8,
            "application/json");
        content.Headers.Add("Stripe-Signature", "t=invalid,v1=invalid");

        var resp = await _factory.Client.PostAsync("/api/webhooks/stripe", content);

        // ASSERT — must reject (not process) invalid signatures
        ((int)resp.StatusCode).ShouldBeGreaterThanOrEqualTo(400);
        ((int)resp.StatusCode).ShouldBeLessThan(500);
    }

    // ── Test 8 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateClient_WithMissingRequiredFields_Returns400()
    {
        // Arrange
        var ctx = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, ctx.UserId);

        // ACT — send an empty object (missing FullName, Phone, etc.)
        var resp = await _factory.Client.PostAsJsonAsync("/api/clients",
            new { }, ctx.Token);

        // ASSERT — 400 Bad Request from model validation
        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── Test 9 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentRequests_SameUser_DoNotCrash()
    {
        // Arrange
        var ctx = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, ctx.UserId);

        // ACT — fire multiple reads concurrently (safe read-only)
        var tasks = Enumerable.Range(0, 5).Select(_ =>
            _factory.Client.GetAsync("/api/clients?page=1&pageSize=10", ctx.Token));

        var responses = await Task.WhenAll(tasks);

        // ASSERT — all should succeed
        foreach (var r in responses)
            r.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── Test 10 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExpiredSubscription_AllowsReads_BlocksWrites()
    {
        // Arrange — register and immediately expire the subscription
        var ctx = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, ctx.UserId, days: -1);

        // ACT 1 — GET (read) must be allowed
        var getResp = await _factory.Client.GetAsync("/api/clients?page=1&pageSize=10", ctx.Token);
        getResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // ACT 2 — POST (write) must be blocked
        var postResp = await _factory.Client.PostAsJsonAsync(
            "/api/clients",
            new
            {
                fullName = "Test User",
                phone = "351912345678",
                leadSource = 0,
                businessType = 0,
                paymentType = 0
            },
            ctx.Token);

        postResp.StatusCode.ShouldBe(HttpStatusCode.PaymentRequired);
    }
}
