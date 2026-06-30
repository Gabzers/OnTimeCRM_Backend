using System.Net;
using System.Net.Http.Json;
using Shouldly;
using OnTime.Application.DTOs.Goals;
using OnTime.Application.DTOs.Permissions;
using OnTime.Application.DTOs.Proposals;
using OnTime.Application.DTOs.Sales;
using OnTime.Domain.Enums;
using OnTime.Tests.Infrastructure;

namespace OnTime.Tests.Flows;

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

    // ── Test 5 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Goals_WeeklyGoal_OnlyCountsSalesInsideItsOwnWindow_NotTheWholeMonth()
    {
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        // A weekly goal starting today — its window is [today, today+7d).
        var todayUtc = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);
        var req = new CreateUserGoalRequest(
            MetricType: (int)GoalMetricType.Sales,
            Period: (int)GoalPeriod.Weekly,
            TargetValue: 10m,
            StartDate: todayUtc,
            EndDate: null
        );
        var createResp = await _factory.Client.PostAsJsonAsync("/api/goals", req, auth.Token);
        createResp.StatusCode.ShouldBe(HttpStatusCode.Created);

        // One sale inside the week, backdated 10 days (still this calendar month) — must NOT count.
        var (_, oldProposalId) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);
        var oldSaleReq = new ConvertToSaleRequest(
            SoldAt: DateTimeOffset.UtcNow.AddDays(-10), FinalValue: 15000m,
            PaymentType: (int)PaymentType.Cash, ModelId: null, FreeTextModel: "Car",
            Plate: null, Chassis: null, Obs: null
        );
        await _factory.Client.PostAsJsonAsync($"/api/proposals/{oldProposalId}/convert", oldSaleReq, auth.Token);

        // One sale inside the week — must count.
        var (_, newProposalId) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);
        var newSaleReq = new ConvertToSaleRequest(
            SoldAt: DateTimeOffset.UtcNow, FinalValue: 15000m,
            PaymentType: (int)PaymentType.Cash, ModelId: null, FreeTextModel: "Car",
            Plate: null, Chassis: null, Obs: null
        );
        await _factory.Client.PostAsJsonAsync($"/api/proposals/{newProposalId}/convert", newSaleReq, auth.Token);

        var listResp = await _factory.Client.GetAsync("/api/goals", auth.Token);
        var list = await listResp.Content.ReadFromJsonAsync<List<GoalProgressDto>>();

        // Only the in-window sale counts — proves progress is scoped to the goal's own dates,
        // not a blanket "this calendar month" snapshot shared by every period.
        list![0].CurrentValue.ShouldBe(1m);
    }

    // ── Test 6 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Goals_SupportsAnnualPeriod_AndShowOnDashboardFlag()
    {
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        var startOfYear = new DateTimeOffset(DateTimeOffset.UtcNow.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var req = new CreateUserGoalRequest(
            MetricType: (int)GoalMetricType.Sales,
            Period: (int)GoalPeriod.Annual,
            TargetValue: 50m,
            StartDate: startOfYear,
            EndDate: null,
            ShowOnDashboard: true
        );

        var createResp = await _factory.Client.PostAsJsonAsync("/api/goals", req, auth.Token);
        createResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<UserGoalDto>();
        created!.Period.ShouldBe((int)GoalPeriod.Annual);
        created.ShowOnDashboard.ShouldBeTrue();

        // A sale made today (within the year) must count toward the annual goal.
        var (_, proposalId) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);
        var saleReq = new ConvertToSaleRequest(
            SoldAt: DateTimeOffset.UtcNow, FinalValue: 15000m,
            PaymentType: (int)PaymentType.Cash, ModelId: null, FreeTextModel: "Car",
            Plate: null, Chassis: null, Obs: null
        );
        await _factory.Client.PostAsJsonAsync($"/api/proposals/{proposalId}/convert", saleReq, auth.Token);

        var listResp = await _factory.Client.GetAsync("/api/goals", auth.Token);
        var list = await listResp.Content.ReadFromJsonAsync<List<GoalProgressDto>>();
        list![0].CurrentValue.ShouldBe(1m);
        list[0].Goal.ShowOnDashboard.ShouldBeTrue();
    }

    // ── Test 7 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task User_CannotUpdateOrDelete_AnotherUsersGoal()
    {
        var owner = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, owner.UserId);
        var stranger = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, stranger.UserId);

        var createReq = new CreateUserGoalRequest(
            MetricType: (int)GoalMetricType.Sales,
            Period: (int)GoalPeriod.Monthly,
            TargetValue: 5m,
            StartDate: DateTimeOffset.UtcNow,
            EndDate: null
        );
        var createResp = await _factory.Client.PostAsJsonAsync("/api/goals", createReq, owner.Token);
        var goal = await createResp.Content.ReadFromJsonAsync<UserGoalDto>();

        var updateResp = await _factory.Client.PutAsJsonAsync(
            $"/api/goals/{goal!.Id}",
            new UpdateUserGoalRequest(TargetValue: 999m, StartDate: goal.StartDate, EndDate: null),
            stranger.Token);
        updateResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var deleteResp = await _factory.Client.DeleteAsync($"/api/goals/{goal.Id}", stranger.Token);
        deleteResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Confirm it's untouched — the owner still sees their original target value.
        var ownerGoals = await _factory.Client.GetFromJsonAsync<List<GoalProgressDto>>("/api/goals", owner.Token);
        ownerGoals!.Single(g => g.Goal.Id == goal.Id).Goal.TargetValue.ShouldBe(5m);
    }

    // ── Conversion rate target range ────────────────────────────────────────

    [Fact]
    public async Task ConversionRateGoal_TargetOver100_Returns422()
    {
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        var req = new CreateUserGoalRequest(
            MetricType: (int)GoalMetricType.ConversionRate, Period: (int)GoalPeriod.Monthly,
            TargetValue: 150m, StartDate: DateTimeOffset.UtcNow, EndDate: null);

        var resp = await _factory.Client.PostAsJsonAsync("/api/goals", req, auth.Token);
        resp.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await resp.Content.ReadAsStringAsync();
        body.ShouldContain("GOAL_PERCENT_OUT_OF_RANGE");
    }

    [Fact]
    public async Task ConversionRateGoal_UpdateOver100_Returns422()
    {
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        var createResp = await _factory.Client.PostAsJsonAsync("/api/goals", new CreateUserGoalRequest(
            MetricType: (int)GoalMetricType.ConversionRate, Period: (int)GoalPeriod.Monthly,
            TargetValue: 50m, StartDate: DateTimeOffset.UtcNow, EndDate: null), auth.Token);
        var created = await createResp.Content.ReadFromJsonAsync<UserGoalDto>();

        var updateResp = await _factory.Client.PutAsJsonAsync(
            $"/api/goals/{created!.Id}",
            new UpdateUserGoalRequest(TargetValue: 101m, StartDate: created.StartDate, EndDate: null),
            auth.Token);
        updateResp.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // ── Reorder ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Goals_Reorder_ChangesListingOrder()
    {
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        async Task<Guid> CreateAsync(decimal target) =>
            (await (await _factory.Client.PostAsJsonAsync("/api/goals", new CreateUserGoalRequest(
                MetricType: (int)GoalMetricType.Sales, Period: (int)GoalPeriod.Monthly,
                TargetValue: target, StartDate: DateTimeOffset.UtcNow, EndDate: null), auth.Token))
                .Content.ReadFromJsonAsync<UserGoalDto>())!.Id;

        var first = await CreateAsync(1m);
        var second = await CreateAsync(2m);
        var third = await CreateAsync(3m);

        // New goals are created at the end — confirm the natural creation order first.
        var initial = await _factory.Client.GetFromJsonAsync<List<GoalProgressDto>>("/api/goals", auth.Token);
        initial!.Select(g => g.Goal.Id).ShouldBe([first, second, third]);

        // Move the third goal to the front.
        var reorderResp = await _factory.Client.PutAsJsonAsync(
            "/api/goals/reorder", new ReorderGoalsRequest([third, first, second]), auth.Token);
        reorderResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var reordered = await _factory.Client.GetFromJsonAsync<List<GoalProgressDto>>("/api/goals", auth.Token);
        reordered!.Select(g => g.Goal.Id).ShouldBe([third, first, second]);
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
