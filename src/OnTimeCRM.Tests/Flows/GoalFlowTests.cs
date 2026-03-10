using System.Net;
using System.Net.Http.Json;
using Shouldly;
using OnTimeCRM.Application.DTOs.Goals;
using OnTimeCRM.Application.DTOs.Permissions;
using OnTimeCRM.Application.DTOs.Proposals;
using OnTimeCRM.Application.DTOs.Sales;
using OnTimeCRM.Domain.Enums;
using OnTimeCRM.Tests.Infrastructure;

namespace OnTimeCRM.Tests.Flows;

/// <summary>
/// Flow 8 — Goals (Objectives)
/// Goal: CRUD for user goals; progress calculation reflects real KPI data.
/// </summary>
[Collection("Integration")]
public class GoalFlowTests : IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public GoalFlowTests(TestWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Test 1 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Goals_CreateAndList_ReturnsGoalWithZeroProgress()
    {
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        // Create a Sales goal for this month
        var req = new CreateUserGoalRequest(
            MetricType: (int)GoalMetricType.Sales,
            Period: (int)GoalPeriod.Monthly,
            TargetValue: 5m,
            StartDate: new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate: null
        );

        var createResp = await _factory.Client.PostAsJsonAsync("/api/goals", req, auth.Token);
        createResp.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await createResp.Content.ReadFromJsonAsync<UserGoalDto>();
        created.ShouldNotBeNull();
        created!.MetricType.ShouldBe((int)GoalMetricType.Sales);
        created.Period.ShouldBe((int)GoalPeriod.Monthly);
        created.TargetValue.ShouldBe(5m);

        // List goals — should show 1 with 0 progress
        var listResp = await _factory.Client.GetAsync("/api/goals", auth.Token);
        listResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var list = await listResp.Content.ReadFromJsonAsync<List<GoalProgressDto>>();
        list.ShouldNotBeNull();
        list!.Count.ShouldBe(1);
        list[0].CurrentValue.ShouldBe(0m);
        list[0].ProgressPct.ShouldBe(0m);
        list[0].Goal.Id.ShouldBe(created.Id);
    }

    // ── Test 2 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Goals_AfterSale_ProgressIncrements()
    {
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        // Create goal: 3 sales this month
        var startOfMonth = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var req = new CreateUserGoalRequest(
            MetricType: (int)GoalMetricType.Sales,
            Period: (int)GoalPeriod.Monthly,
            TargetValue: 3m,
            StartDate: startOfMonth,
            EndDate: null
        );
        var createResp = await _factory.Client.PostAsJsonAsync("/api/goals", req, auth.Token);
        createResp.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Make 2 sales
        var (_, p1Id) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);
        var (_, p2Id) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);

        var saleReq = new ConvertToSaleRequest(
            SoldAt: DateTimeOffset.UtcNow, FinalValue: 15000m,
            PaymentType: (int)PaymentType.Cash, ModelId: null, FreeTextModel: "Car",
            Plate: null, Chassis: null, Obs: null
        );
        await _factory.Client.PostAsJsonAsync($"/api/proposals/{p1Id}/convert", saleReq, auth.Token);
        await _factory.Client.PostAsJsonAsync($"/api/proposals/{p2Id}/convert", saleReq, auth.Token);

        // Check goal progress
        var listResp = await _factory.Client.GetAsync("/api/goals", auth.Token);
        listResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var list = await listResp.Content.ReadFromJsonAsync<List<GoalProgressDto>>();
        list.ShouldNotBeNull();
        var progress = list![0];
        progress.CurrentValue.ShouldBe(2m);
        progress.ProgressPct.ShouldBeGreaterThan(0m);
        progress.ProgressPct.ShouldBeLessThanOrEqualTo(100m);
    }

    // ── Test 3 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Goals_UpdateAndDelete_WorkCorrectly()
    {
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        // Create goal
        var req = new CreateUserGoalRequest(
            MetricType: (int)GoalMetricType.NewClients,
            Period: (int)GoalPeriod.Weekly,
            TargetValue: 7m,
            StartDate: DateTimeOffset.UtcNow,
            EndDate: null
        );
        var createResp = await _factory.Client.PostAsJsonAsync("/api/goals", req, auth.Token);
        var created = await createResp.Content.ReadFromJsonAsync<UserGoalDto>();
        created.ShouldNotBeNull();

        // Update: change target value
        var updateReq = new UpdateUserGoalRequest(
            TargetValue: 10m,
            StartDate: created!.StartDate,
            EndDate: DateTimeOffset.UtcNow.AddDays(7)
        );
        var updateResp = await _factory.Client.PutAsJsonAsync($"/api/goals/{created.Id}", updateReq, auth.Token);
        updateResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await updateResp.Content.ReadFromJsonAsync<UserGoalDto>();
        updated!.TargetValue.ShouldBe(10m);
        updated.EndDate.ShouldNotBeNull();

        // Delete
        var deleteResp = await _factory.Client.DeleteAsync($"/api/goals/{created.Id}", auth.Token);
        deleteResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify gone
        var listResp = await _factory.Client.GetAsync("/api/goals", auth.Token);
        var list = await listResp.Content.ReadFromJsonAsync<List<GoalProgressDto>>();
        list!.Count.ShouldBe(0);
    }

    // ── Test 4 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Permissions_GetByRole_ReturnsSeedDefaults()
    {
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        // Manager should get all permissions seeded
        var managerResp = await _factory.Client.GetAsync("/api/permissions?role=1", auth.Token);
        managerResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var managerPerms = await managerResp.Content.ReadFromJsonAsync<List<MenuPermissionDto>>();
        managerPerms.ShouldNotBeNull();
        managerPerms!.Count.ShouldBeGreaterThan(0);
        // Managers have all permissions
        managerPerms.All(p => p.CanView).ShouldBeTrue();

        // Salesperson — should also be seeded
        var salesResp = await _factory.Client.GetAsync("/api/permissions?role=0", auth.Token);
        salesResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var salesPerms = await salesResp.Content.ReadFromJsonAsync<List<MenuPermissionDto>>();
        salesPerms.ShouldNotBeNull();
        salesPerms!.Count.ShouldBeGreaterThan(0);
    }
}
