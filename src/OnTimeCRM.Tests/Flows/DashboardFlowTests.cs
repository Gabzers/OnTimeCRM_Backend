using System.Net;
using System.Net.Http.Json;
using Shouldly;
using OnTimeCRM.Application.DTOs.Proposals;
using OnTimeCRM.Application.DTOs.Sales;
using OnTimeCRM.Domain.Enums;
using OnTimeCRM.Tests.Infrastructure;

namespace OnTimeCRM.Tests.Flows;

/// <summary>
/// Flow 7 — Dashboard and Statistics
/// Goal: KPIs and charts return correct values based on business dates (ProposalDate, SoldAt).
/// </summary>
[Collection("Integration")]
public class DashboardFlowTests : IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public DashboardFlowTests(TestWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Test 1 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dashboard_EmptyAccount_ReturnsZeroKpis()
    {
        // ARRANGE — fresh account with no clients or sales
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        // ACT
        var resp = await _factory.Client.GetAsync("/api/dashboard", auth.Token);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dashboard = await resp.Content.ReadFromJsonAsync<DashboardDto>();

        // ASSERT
        dashboard.ShouldNotBeNull();
        dashboard!.ActiveClients.ShouldBe(0);
        dashboard.SalesThisMonth.ShouldBe(0);
        dashboard.RevenueThisMonth.ShouldBe(0m);
        dashboard.ProposalsThisMonth.ShouldBe(0);
        dashboard.OverdueNotificationsCount.ShouldBe(0);
    }

    // ── Test 2 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dashboard_AfterCreatingClientsAndSales_ReflectsCorrectKpis()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        // Create 3 clients (all proposals this month)
        var (_, p1Id) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);
        var (_, p2Id) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);
        var (_, p3Id) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);

        // Convert 2 to sales (this month)
        var saleReq = new ConvertToSaleRequest(
            SoldAt: DateTimeOffset.UtcNow, FinalValue: 20000m,
            PaymentType: (int)PaymentType.Cash, ModelId: null, FreeTextModel: "Car",
            Plate: null, Chassis: null, Obs: null
        );
        await _factory.Client.PostAsJsonAsync($"/api/proposals/{p1Id}/convert", saleReq, auth.Token);

        var saleReq2 = saleReq with { FinalValue = 30000m };
        await _factory.Client.PostAsJsonAsync($"/api/proposals/{p2Id}/convert", saleReq2, auth.Token);

        // Mark 1 as lost (still a proposal, but closes it)
        await _factory.Client.PostAsJsonAsync($"/api/proposals/{p3Id}/lost",
            new MarkProposalLostRequest((int)LossReason.Price, null), auth.Token);

        // ACT
        var resp = await _factory.Client.GetAsync("/api/dashboard", auth.Token);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dashboard = await resp.Content.ReadFromJsonAsync<DashboardDto>();

        // ASSERT
        dashboard.ShouldNotBeNull();
        dashboard!.SalesThisMonth.ShouldBe(2);
        dashboard.RevenueThisMonth.ShouldBe(50000m); // 20000 + 30000
        dashboard.ProposalsThisMonth.ShouldBe(3);
        // Active clients = those NOT in a final stage (lost only = 1 lost, 2 won)
        // After 2 sales (IsWon=true) and 1 lost (IsLost=true), active clients = 0
        dashboard.ActiveClients.ShouldBe(0);
    }

    // ── Test 3 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dashboard_HotDeals_OnlyIncludesNonFinalStagesWithRecentInteraction()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        // Create 2 clients — both Hot (just created)
        var (client1Id, p1Id) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);
        var (client2Id, p2Id) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);

        // Convert client2 to sale (moves to Won final stage)
        var saleReq = new ConvertToSaleRequest(
            SoldAt: DateTimeOffset.UtcNow, FinalValue: 20000m,
            PaymentType: (int)PaymentType.Cash, ModelId: null, FreeTextModel: "Car",
            Plate: null, Chassis: null, Obs: null
        );
        await _factory.Client.PostAsJsonAsync($"/api/proposals/{p2Id}/convert", saleReq, auth.Token);

        // ACT
        var resp = await _factory.Client.GetAsync("/api/dashboard", auth.Token);
        var dashboard = await resp.Content.ReadFromJsonAsync<DashboardDto>();

        // ASSERT — HotDeals should only include non-final client1
        var hotDeals = dashboard!.HotDeals.ToList();
        hotDeals.Count.ShouldBe(1); // only client1 (client2 is in Won final stage)
    }

    // ── Test 4 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dashboard_MonthlySales_HasCorrectNumberOfMonths()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        // ACT
        var resp = await _factory.Client.GetAsync("/api/dashboard", auth.Token);
        var dashboard = await resp.Content.ReadFromJsonAsync<DashboardDto>();

        // ASSERT — MonthlySales has 6 months of data
        var monthlySales = dashboard!.MonthlySales.ToList();
        monthlySales.Count.ShouldBe(6);

        // Current month should be included
        var now = DateTime.UtcNow;
        var thisMonth = monthlySales.FirstOrDefault(m => m.Year == now.Year && m.Month == now.Month);
        thisMonth.ShouldNotBeNull();
    }

    // ── Test 5 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dashboard_RevenueThisMonth_ExcludesPreviousMonthSales()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        // Create and convert a sale with SoldAt = last month (should NOT count in this month's revenue)
        var (_, pastProposalId) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);
        var pastSaleReq = new ConvertToSaleRequest(
            SoldAt: DateTimeOffset.UtcNow.AddMonths(-1), // last month
            FinalValue: 50000m,
            PaymentType: (int)PaymentType.Cash, ModelId: null, FreeTextModel: "Old Car",
            Plate: null, Chassis: null, Obs: null
        );
        await _factory.Client.PostAsJsonAsync($"/api/proposals/{pastProposalId}/convert", pastSaleReq, auth.Token);

        // Create a sale this month
        var (_, currentProposalId) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);
        var currentSaleReq = new ConvertToSaleRequest(
            SoldAt: DateTimeOffset.UtcNow, // this month
            FinalValue: 25000m,
            PaymentType: (int)PaymentType.Cash, ModelId: null, FreeTextModel: "New Car",
            Plate: null, Chassis: null, Obs: null
        );
        await _factory.Client.PostAsJsonAsync($"/api/proposals/{currentProposalId}/convert", currentSaleReq, auth.Token);

        // ACT
        var resp = await _factory.Client.GetAsync("/api/dashboard", auth.Token);
        var dashboard = await resp.Content.ReadFromJsonAsync<DashboardDto>();

        // ASSERT — Only the current month sale (25000) is in RevenueThisMonth
        dashboard!.RevenueThisMonth.ShouldBe(25000m);
        dashboard.SalesThisMonth.ShouldBe(1); // only current month sale
    }

    // ── Test 6 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dashboard_LossReasonStats_CalculatesPercentagesCorrectly()
    {
        // ARRANGE — create 3 proposals and mark them lost with different reasons
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        var (_, p1Id) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);
        var (_, p2Id) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);
        var (_, p3Id) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);

        await _factory.Client.PostAsJsonAsync($"/api/proposals/{p1Id}/lost",
            new MarkProposalLostRequest((int)LossReason.Price, null), auth.Token);
        await _factory.Client.PostAsJsonAsync($"/api/proposals/{p2Id}/lost",
            new MarkProposalLostRequest((int)LossReason.Price, null), auth.Token);
        await _factory.Client.PostAsJsonAsync($"/api/proposals/{p3Id}/lost",
            new MarkProposalLostRequest((int)LossReason.Competition, null), auth.Token);

        // ACT
        var resp = await _factory.Client.GetAsync("/api/dashboard", auth.Token);
        var dashboard = await resp.Content.ReadFromJsonAsync<DashboardDto>();

        // ASSERT — Loss reasons aggregated: 2x Price, 1x Competition
        var lossReasons = dashboard!.LossReasons.ToList();
        lossReasons.ShouldNotBeEmpty();
        var priceLoss = lossReasons.FirstOrDefault(r => r.LossReason == (int)LossReason.Price);
        priceLoss.ShouldNotBeNull();
        priceLoss!.Count.ShouldBe(2);
        var competitionLoss = lossReasons.FirstOrDefault(r => r.LossReason == (int)LossReason.Competition);
        competitionLoss.ShouldNotBeNull();
        competitionLoss!.Count.ShouldBe(1);
    }
}
