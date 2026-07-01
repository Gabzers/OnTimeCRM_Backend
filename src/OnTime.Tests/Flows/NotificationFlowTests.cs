using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using OnTime.Application.DTOs.Clients;
using OnTime.Application.DTOs.Notifications;
using OnTime.Application.DTOs.Proposals;
using OnTime.Application.DTOs.Stages;
using OnTime.Domain.Enums;
using OnTime.Tests.Infrastructure;

namespace OnTime.Tests.Flows;

/// <summary>
/// Flow 5 — Notifications and Follow-ups
/// Goal: Notifications are created, delivered, managed, and follow the preference settings.
/// </summary>
[Collection("Integration")]
public class NotificationFlowTests : IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public NotificationFlowTests(TestWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Test 1 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task AutoNotification_CreatedOnStageChange_AppearsTodayIfDaysAfterIsZero()
    {
        // ARRANGE — add a template with DaysAfter=0 to the first non-final stage
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var stages = await _factory.Client.GetFromJsonAsync<IEnumerable<ClientStageDto>>("/api/stages", auth.Token);
        var thirdStage = stages!.OrderBy(s => s.Order).ElementAt(2); // "Agendar Test Drive"

        // Add a template with DaysAfter=0 → notification due today
        var templateReq = new CreateStageTemplateRequest(Title: "Immediate follow-up", DaysAfter: 0);
        var addTemplateResp = await _factory.Client.PostAsJsonAsync(
            $"/api/stages/{thirdStage.Id}/templates", templateReq, auth.Token);
        addTemplateResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Create client and move to that stage
        var (clientId, _) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);
        var stageChangeResp = await _factory.Client.PutAsJsonAsync(
            $"/api/clients/{clientId}/stage",
            new UpdateClientStageRequest(StageId: thirdStage.Id, Obs: null), auth.Token);
        stageChangeResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // ASSERT — notification appears in /today
        var todayResp = await _factory.Client.GetAsync("/api/notifications/today", auth.Token);
        todayResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var todayNotifications = await todayResp.Content.ReadFromJsonAsync<IEnumerable<NotificationDto>>();
        todayNotifications!.ShouldContain(n =>
            n.ClientId == clientId && n.Trigger == (int)NotificationTrigger.StageChanged);
    }

    // ── Test 2 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ManualNotification_CreatedWithSpecificDate_AppearsOnCorrectDay()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (clientId, _) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);

        var futureDate = DateTimeOffset.UtcNow.AddDays(7);
        var req = new CreateNotificationRequest(
            ClientId: clientId,
            ProposalId: null,
            SaleId: null,
            Title: "Follow-up in 1 week",
            Body: "Call to check if they decided",
            ScheduledFor: futureDate
        );

        // ACT
        var response = await _factory.Client.PostAsJsonAsync("/api/notifications", req, auth.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var notification = await response.Content.ReadFromJsonAsync<NotificationDto>();

        // ASSERT — notification exists at the correct date
        notification.ShouldNotBeNull();
        notification!.ScheduledFor.Date.ShouldBe(futureDate.Date);
        notification.Status.ShouldBe((int)NotificationStatus.Pending);

        // ASSERT — does NOT appear in /today (it's 7 days in the future)
        var todayResp = await _factory.Client.GetAsync("/api/notifications/today", auth.Token);
        var todayList = await todayResp.Content.ReadFromJsonAsync<IEnumerable<NotificationDto>>();
        todayList!.ShouldNotContain(n => n.Id == notification.Id);
    }

    // ── Test 3 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task TodayNotifications_ReturnsOverdueAndTodayOnly_NotFuture()
    {
        // ARRANGE — create 3 notifications at different times
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (clientId, _) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);

        // Directly insert notifications with specific dates in DB
        var notifYesterday = new OnTime.Domain.Entities.Notification
        {
            UserId = auth.UserId,
            ClientId = clientId,
            Title = "Overdue",
            ScheduledFor = DateTimeOffset.UtcNow.AddDays(-1),
            Status = NotificationStatus.Pending,
            Trigger = NotificationTrigger.Manual
        };
        var notifToday = new OnTime.Domain.Entities.Notification
        {
            UserId = auth.UserId,
            ClientId = clientId,
            Title = "Today",
            ScheduledFor = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero),
            Status = NotificationStatus.Pending,
            Trigger = NotificationTrigger.Manual
        };
        var notifTomorrow = new OnTime.Domain.Entities.Notification
        {
            UserId = auth.UserId,
            ClientId = clientId,
            Title = "Future",
            ScheduledFor = DateTimeOffset.UtcNow.AddDays(1),
            Status = NotificationStatus.Pending,
            Trigger = NotificationTrigger.Manual
        };

        _factory.Db.Notifications.AddRange(notifYesterday, notifToday, notifTomorrow);
        await _factory.Db.SaveChangesAsync();
        _factory.Db.ChangeTracker.Clear();

        // ACT
        var todayResp = await _factory.Client.GetAsync("/api/notifications/today", auth.Token);
        todayResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var todayList = (await todayResp.Content.ReadFromJsonAsync<IEnumerable<NotificationDto>>())!;

        // ASSERT — yesterday (overdue) and today appear; future does NOT
        todayList.ShouldContain(n => n.Id == notifYesterday.Id);
        todayList.ShouldContain(n => n.Id == notifToday.Id);
        todayList.ShouldNotContain(n => n.Id == notifTomorrow.Id);
    }

    // ── Test 4 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkNotificationDone_RemovesFromPendingList_SetsStatusAndDoneAt()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        // Create a notification due today
        var notification = new OnTime.Domain.Entities.Notification
        {
            UserId = auth.UserId,
            Title = "Test done",
            ScheduledFor = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero),
            Status = NotificationStatus.Pending,
            Trigger = NotificationTrigger.Manual
        };
        _factory.Db.Notifications.Add(notification);
        await _factory.Db.SaveChangesAsync();
        _factory.Db.ChangeTracker.Clear();

        // ACT — mark done
        var doneResp = await _factory.Client.PatchAsJsonAsync<object?>(
            $"/api/notifications/{notification.Id}/done", null, auth.Token);
        doneResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // ASSERT — not in today list
        var todayList = await _factory.Client.GetFromJsonAsync<IEnumerable<NotificationDto>>(
            "/api/notifications/today", auth.Token);
        todayList!.ShouldNotContain(n => n.Id == notification.Id);

        // ASSERT — DB status = Done and DoneAt set
        _factory.Db.ChangeTracker.Clear();
        var notifDb = await _factory.Db.Notifications.FindAsync(notification.Id);
        notifDb!.Status.ShouldBe(NotificationStatus.Done);
        notifDb.DoneAt.ShouldNotBeNull();
    }

    // ── Test 5 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SnoozeNotification_UpdatesScheduledFor_KeepsStatusAsPending()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        var notification = new OnTime.Domain.Entities.Notification
        {
            UserId = auth.UserId,
            Title = "Test snooze",
            ScheduledFor = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero),
            Status = NotificationStatus.Pending,
            Trigger = NotificationTrigger.Manual
        };
        _factory.Db.Notifications.Add(notification);
        await _factory.Db.SaveChangesAsync();
        _factory.Db.ChangeTracker.Clear();

        var snoozedUntil = DateTimeOffset.UtcNow.AddDays(2);
        var snoozeReq = new SnoozeNotificationRequest(SnoozedUntil: snoozedUntil);

        // ACT
        var snoozeResp = await _factory.Client.PatchAsJsonAsync(
            $"/api/notifications/{notification.Id}/snooze", snoozeReq, auth.Token);
        snoozeResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // ASSERT — ScheduledFor updated, still Pending
        _factory.Db.ChangeTracker.Clear();
        var notifDb = await _factory.Db.Notifications.FindAsync(notification.Id);
        notifDb!.ScheduledFor.Date.ShouldBe(snoozedUntil.Date);
        notifDb.Status.ShouldBe(NotificationStatus.Pending);

        // ASSERT — no longer appears in /today
        var todayList = await _factory.Client.GetFromJsonAsync<IEnumerable<NotificationDto>>(
            "/api/notifications/today", auth.Token);
        todayList!.ShouldNotContain(n => n.Id == notification.Id);
    }

    // ── Test 6 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task OverdueCount_ReflectsOnlyPendingPastDue()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        // Add 2 overdue pending + 1 done overdue + 1 future pending
        _factory.Db.Notifications.AddRange(
            new() { UserId = auth.UserId, Title = "Overdue1", ScheduledFor = DateTimeOffset.UtcNow.AddDays(-3), Status = NotificationStatus.Pending, Trigger = NotificationTrigger.Manual },
            new() { UserId = auth.UserId, Title = "Overdue2", ScheduledFor = DateTimeOffset.UtcNow.AddDays(-1), Status = NotificationStatus.Pending, Trigger = NotificationTrigger.Manual },
            new() { UserId = auth.UserId, Title = "DoneOverdue", ScheduledFor = DateTimeOffset.UtcNow.AddDays(-2), Status = NotificationStatus.Done, Trigger = NotificationTrigger.Manual },
            new() { UserId = auth.UserId, Title = "Future", ScheduledFor = DateTimeOffset.UtcNow.AddDays(5), Status = NotificationStatus.Pending, Trigger = NotificationTrigger.Manual }
        );
        await _factory.Db.SaveChangesAsync();
        _factory.Db.ChangeTracker.Clear();

        // ACT
        var countResp = await _factory.Client.GetAsync("/api/notifications/overdue-count", auth.Token);
        countResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var count = await countResp.Content.ReadFromJsonAsync<int>();

        // ASSERT — only the 2 pending+overdue are counted (not done, not future)
        count.ShouldBe(2);
    }

    // ── Test 7 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaleClosedNotification_ScheduledFor30DaysAfter_ByDefault()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (clientId, proposalId) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);

        // ACT — convert proposal to sale
        var convertReq = new ConvertToSaleRequest(
            SoldAt: DateTimeOffset.UtcNow, FinalValue: 25000m,
            PaymentType: (int)PaymentType.Cash, ModelId: null, FreeTextModel: "Car",
            Plate: null, Chassis: null, Obs: null
        );
        await _factory.Client.PostAsJsonAsync($"/api/proposals/{proposalId}/convert", convertReq, auth.Token);

        // ASSERT — post-sale notification scheduled 30 days from now
        _factory.Db.ChangeTracker.Clear();
        var notification = await _factory.Db.Notifications
            .FirstOrDefaultAsync(n => n.ClientId == clientId && n.Trigger == NotificationTrigger.SaleClosed);
        notification.ShouldNotBeNull();
        notification!.ScheduledFor.Date.ShouldBe(DateTime.UtcNow.AddDays(30).Date);
    }

    // ── Test 8 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task NotificationPreference_WhenSaleFollowUpDaysChanged_AffectsNewNotifications()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        // Change SaleFollowUpDays from 30 to 7
        var prefsResp = await _factory.Client.GetFromJsonAsync<NotificationPreferenceDto>(
            "/api/preferences/notifications", auth.Token);
        prefsResp.ShouldNotBeNull();

        var updatePrefsReq = new UpdateNotificationPreferenceRequest(
            DailyDigestTime: prefsResp!.DailyDigestTime,
            DigestFrequencyDays: prefsResp.DigestFrequencyDays,
            DigestDaysOfWeek: prefsResp.DigestDaysOfWeek,
            SaleFollowUpDays: 7,
            DigestEnabled: prefsResp.DigestEnabled,
            StageChangeNotificationsEnabled: prefsResp.StageChangeNotificationsEnabled,
            SaleNotificationsEnabled: prefsResp.SaleNotificationsEnabled
        );
        var updateResp = await _factory.Client.PutAsJsonAsync(
            "/api/preferences/notifications", updatePrefsReq, auth.Token);
        updateResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // ACT — create a sale
        var (clientId, proposalId) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);
        var convertReq = new ConvertToSaleRequest(
            SoldAt: DateTimeOffset.UtcNow, FinalValue: 20000m,
            PaymentType: (int)PaymentType.Cash, ModelId: null, FreeTextModel: "Car",
            Plate: null, Chassis: null, Obs: null
        );
        await _factory.Client.PostAsJsonAsync($"/api/proposals/{proposalId}/convert", convertReq, auth.Token);

        // ASSERT — post-sale notification is 7 days, not 30
        _factory.Db.ChangeTracker.Clear();
        var notification = await _factory.Db.Notifications
            .FirstOrDefaultAsync(n => n.ClientId == clientId && n.Trigger == NotificationTrigger.SaleClosed);
        notification.ShouldNotBeNull();
        notification!.ScheduledFor.Date.ShouldBe(DateTime.UtcNow.AddDays(7).Date);
    }

    // ── Test 9 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task User_CannotMarkDoneSnoozeOrIgnore_AnotherUsersNotification()
    {
        var owner = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, owner.UserId);
        var stranger = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, stranger.UserId);

        var notification = new OnTime.Domain.Entities.Notification
        {
            UserId = owner.UserId,
            Title = "Owner's notification",
            ScheduledFor = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero),
            Status = NotificationStatus.Pending,
            Trigger = NotificationTrigger.Manual
        };
        _factory.Db.Notifications.Add(notification);
        await _factory.Db.SaveChangesAsync();
        _factory.Db.ChangeTracker.Clear();

        var doneResp = await _factory.Client.PatchAsJsonAsync<object?>(
            $"/api/notifications/{notification.Id}/done", null, stranger.Token);
        doneResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var snoozeResp = await _factory.Client.PatchAsJsonAsync(
            $"/api/notifications/{notification.Id}/snooze",
            new SnoozeNotificationRequest(SnoozedUntil: DateTimeOffset.UtcNow.AddDays(1)), stranger.Token);
        snoozeResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var ignoreResp = await _factory.Client.PatchAsJsonAsync<object?>(
            $"/api/notifications/{notification.Id}/ignore", null, stranger.Token);
        ignoreResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Confirm it's untouched — still pending, never marked done by the stranger's attempt.
        _factory.Db.ChangeTracker.Clear();
        var notifDb = await _factory.Db.Notifications.FindAsync(notification.Id);
        notifDb!.Status.ShouldBe(NotificationStatus.Pending);
    }
}
