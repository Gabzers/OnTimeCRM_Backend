using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using OnTime.Application.Interfaces;
using OnTime.Domain.Entities;
using OnTime.Domain.Enums;
using OnTime.Tests.Infrastructure;

namespace OnTime.Tests.Flows;

/// <summary>
/// Flow — the two non-email passes of the internal /run-scheduled-jobs cron endpoint:
/// stage-driven temperature transitions (Pass 1) and recurring notification generation (Pass 2).
/// See 04-DECISIONS/2026-06-30-stage-driven-temperature-and-notifications.md.
/// </summary>
[Collection("Integration")]
public class ScheduledJobsFlowTests : IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public ScheduledJobsFlowTests(TestWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static HttpRequestMessage RunJobsRequest()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/internal/run-scheduled-jobs");
        req.Headers.Add("X-Internal-Key", "test-internal-jobs-secret");
        return req;
    }

    // ── Test 1 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task TemperatureTransition_AppliesRuleBasedOnDaysSinceEntry()
    {
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        var (clientId, _) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);

        var client = await _factory.Db.Clients.FirstAsync(c => c.Id == clientId);
        var stage = await _factory.Db.ClientStages.FirstAsync(s => s.Id == client.CurrentStageId);

        stage.AffectsTemperature = true;
        _factory.Db.ClientStageTemperatureRules.Add(new ClientStageTemperatureRule
        {
            StageId = stage.Id,
            DaysAfterEntry = 0,
            Temperature = (int)DealTemperature.Cold
        });

        // Client "entered" the stage 10 days ago and starts out Hot — the rule should flip it Cold.
        client.LastInteractionAt = DateTimeOffset.UtcNow.AddDays(-10);
        client.Temperature = DealTemperature.Hot;
        await _factory.Db.SaveChangesAsync();
        _factory.Db.ChangeTracker.Clear();

        var resp = await _factory.Client.SendAsync(RunJobsRequest());
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<ScheduledJobsRunResult>();
        result!.TemperatureTransitions.ShouldBeGreaterThanOrEqualTo(1);

        _factory.Db.ChangeTracker.Clear();
        var clientAfter = await _factory.Db.Clients.FirstAsync(c => c.Id == clientId);
        clientAfter.Temperature.ShouldBe(DealTemperature.Cold);
    }

    // ── Test 2 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecurringNotification_GeneratesOccurrence_WhenIntervalElapsed()
    {
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        var (clientId, _) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);
        var client = await _factory.Db.Clients.FirstAsync(c => c.Id == clientId);

        var template = new StageNotificationTemplate
        {
            StageId = client.CurrentStageId,
            UserId = auth.UserId,
            Title = "Recurring check-in",
            DaysAfter = 0,
            IsRecurring = true,
            RecurrenceIntervalDays = 3
        };
        _factory.Db.StageNotificationTemplates.Add(template);

        var history = new ClientStageHistory
        {
            ClientId = clientId,
            UserId = auth.UserId,
            ToStageId = client.CurrentStageId
        };
        _factory.Db.ClientStageHistories.Add(history);
        await _factory.Db.SaveChangesAsync();

        // Series last fired 5 days ago with a 3-day interval → next occurrence was due 2 days ago.
        var series = new ClientStageNotificationSeries
        {
            ClientStageHistoryId = history.Id,
            TemplateId = template.Id,
            IsActive = true,
            OccurrenceCount = 1,
            LastFiredAt = DateTimeOffset.UtcNow.AddDays(-5)
        };
        _factory.Db.ClientStageNotificationSeries.Add(series);
        await _factory.Db.SaveChangesAsync();
        _factory.Db.ChangeTracker.Clear();

        var resp = await _factory.Client.SendAsync(RunJobsRequest());
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<ScheduledJobsRunResult>();
        result!.NotificationsGenerated.ShouldBeGreaterThanOrEqualTo(1);

        _factory.Db.ChangeTracker.Clear();
        var notification = await _factory.Db.Notifications
            .FirstOrDefaultAsync(n => n.ClientId == clientId && n.Title == "Recurring check-in");
        notification.ShouldNotBeNull();

        var seriesAfter = await _factory.Db.ClientStageNotificationSeries.FirstAsync(s => s.Id == series.Id);
        seriesAfter.OccurrenceCount.ShouldBe(2);
    }

    // ── Test 3 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecurringNotification_StopsAtMaxOccurrences()
    {
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        var (clientId, _) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);
        var client = await _factory.Db.Clients.FirstAsync(c => c.Id == clientId);

        var template = new StageNotificationTemplate
        {
            StageId = client.CurrentStageId,
            UserId = auth.UserId,
            Title = "Last occurrence",
            DaysAfter = 0,
            IsRecurring = true,
            RecurrenceIntervalDays = 3,
            MaxOccurrences = 2
        };
        _factory.Db.StageNotificationTemplates.Add(template);

        var history = new ClientStageHistory { ClientId = clientId, UserId = auth.UserId, ToStageId = client.CurrentStageId };
        _factory.Db.ClientStageHistories.Add(history);
        await _factory.Db.SaveChangesAsync();

        var series = new ClientStageNotificationSeries
        {
            ClientStageHistoryId = history.Id,
            TemplateId = template.Id,
            IsActive = true,
            OccurrenceCount = 1, // one more occurrence reaches MaxOccurrences=2
            LastFiredAt = DateTimeOffset.UtcNow.AddDays(-5)
        };
        _factory.Db.ClientStageNotificationSeries.Add(series);
        await _factory.Db.SaveChangesAsync();
        _factory.Db.ChangeTracker.Clear();

        var resp = await _factory.Client.SendAsync(RunJobsRequest());
        var result = await resp.Content.ReadFromJsonAsync<ScheduledJobsRunResult>();
        result!.SeriesCompleted.ShouldBeGreaterThanOrEqualTo(1);

        _factory.Db.ChangeTracker.Clear();
        var seriesAfter = await _factory.Db.ClientStageNotificationSeries.FirstAsync(s => s.Id == series.Id);
        seriesAfter.IsActive.ShouldBeFalse();
        seriesAfter.OccurrenceCount.ShouldBe(2);
    }
}
