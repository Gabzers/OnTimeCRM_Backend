using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using OnTime.Application.DTOs.Auth;
using OnTime.Application.DTOs.Notifications;
using OnTime.Application.DTOs.Users;
using OnTime.Application.Interfaces;
using OnTime.Tests.Infrastructure;

namespace OnTime.Tests.Flows;

/// <summary>
/// Flow — Business summary email (3rd email type, separate from the pending-notifications
/// digest), user locale plumbing, and the internal scheduled-jobs endpoint that drives both.
/// </summary>
[Collection("Integration")]
public class BusinessSummaryFlowTests : IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public BusinessSummaryFlowTests(TestWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Test 1 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task NewUser_HasBusinessSummaryDefaults_EnabledWeeklyMondayAllSections()
    {
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);

        var prefs = await _factory.Client.GetFromJsonAsync<NotificationPreferenceDto>(
            "/api/preferences/notifications", auth.Token);

        prefs.ShouldNotBeNull();
        prefs!.BusinessSummaryEnabled.ShouldBeTrue();
        prefs.BusinessSummaryFrequency.ShouldBe(0); // Weekly
        prefs.BusinessSummaryDayOfWeek.ShouldBe(1); // Monday
        prefs.BusinessSummaryIncludeCounts.ShouldBeTrue();
        prefs.BusinessSummaryIncludeStageSummary.ShouldBeTrue();
        prefs.BusinessSummaryIncludeGoals.ShouldBeTrue();
    }

    // ── Test 2 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateBusinessSummaryPreferences_RoundTrips()
    {
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        var current = await _factory.Client.GetFromJsonAsync<NotificationPreferenceDto>(
            "/api/preferences/notifications", auth.Token);

        var updateReq = new UpdateNotificationPreferenceRequest(
            DailyDigestTime: current!.DailyDigestTime,
            DigestFrequencyDays: current.DigestFrequencyDays,
            DigestDaysOfWeek: current.DigestDaysOfWeek,
            SaleFollowUpDays: current.SaleFollowUpDays,
            DigestEnabled: current.DigestEnabled,
            StageChangeNotificationsEnabled: current.StageChangeNotificationsEnabled,
            SaleNotificationsEnabled: current.SaleNotificationsEnabled,
            BusinessSummaryEnabled: true,
            BusinessSummaryFrequency: 1, // Monthly
            BusinessSummaryDayOfWeek: 3, // Wednesday
            BusinessSummaryIncludeCounts: false,
            BusinessSummaryIncludeStageSummary: true,
            BusinessSummaryIncludeGoals: false
        );

        var updateResp = await _factory.Client.PutAsJsonAsync("/api/preferences/notifications", updateReq, auth.Token);
        updateResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await _factory.Client.GetFromJsonAsync<NotificationPreferenceDto>(
            "/api/preferences/notifications", auth.Token);

        updated!.BusinessSummaryFrequency.ShouldBe(1);
        updated.BusinessSummaryDayOfWeek.ShouldBe(3);
        updated.BusinessSummaryIncludeCounts.ShouldBeFalse();
        updated.BusinessSummaryIncludeStageSummary.ShouldBeTrue();
        updated.BusinessSummaryIncludeGoals.ShouldBeFalse();
    }

    // ── Test 3 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UserLocale_DefaultsToPtPT_AndCanBeUpdatedViaMe()
    {
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);

        var me = await _factory.Client.GetFromJsonAsync<UserDto>("/api/users/me", auth.Token);
        me!.Locale.ShouldBe("pt-PT");

        var updateResp = await _factory.Client.PutAsJsonAsync(
            "/api/users/me", new UpdateUserRequest(FullName: null, Phone: null, Locale: "en-US"), auth.Token);
        updateResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updatedMe = await _factory.Client.GetFromJsonAsync<UserDto>("/api/users/me", auth.Token);
        updatedMe!.Locale.ShouldBe("en-US");
    }

    // ── Test 4 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task FriendRequestEmail_UsesReceiversLocale_NotSendersLocale()
    {
        var sender = await TestHelpers.RegisterManagerAsync(_factory.Client);
        var receiver = await TestHelpers.RegisterManagerAsync(_factory.Client);

        // Receiver prefers English; sender stays on the pt-PT default.
        await _factory.Client.PutAsJsonAsync("/api/users/me", new UpdateUserRequest(FullName: null, Phone: null, Locale: "en-US"), receiver.Token);

        var reqResp = await _factory.Client.PostAsJsonAsync(
            "/api/friends/requests", new { email = receiver.Email }, sender.Token);
        reqResp.StatusCode.ShouldBe(HttpStatusCode.Created);

        var sent = _factory.EmailSender.Sent.ShouldHaveSingleItem();
        sent.ToEmail.ShouldBe(receiver.Email.ToLower()); // emails are stored lower-cased
        sent.Subject.ShouldBe("New friend request — OnTime"); // English, because the RECEIVER is en-US
    }

    // ── Test 5 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task InternalJobsEndpoint_RejectsMissingOrWrongKey_AcceptsCorrectKey()
    {
        var noKeyReq = new HttpRequestMessage(HttpMethod.Post, "/api/internal/run-scheduled-jobs");
        var noKeyResp = await _factory.Client.SendAsync(noKeyReq);
        noKeyResp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var wrongKeyReq = new HttpRequestMessage(HttpMethod.Post, "/api/internal/run-scheduled-jobs");
        wrongKeyReq.Headers.Add("X-Internal-Key", "not-the-real-key");
        var wrongKeyResp = await _factory.Client.SendAsync(wrongKeyReq);
        wrongKeyResp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var okReq = new HttpRequestMessage(HttpMethod.Post, "/api/internal/run-scheduled-jobs");
        okReq.Headers.Add("X-Internal-Key", "test-internal-jobs-secret");
        var okResp = await _factory.Client.SendAsync(okReq);
        okResp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── Test 6 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task BusinessSummary_FiresForUserDueToday_AndDoesNotFireTwiceSameWindow()
    {
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);

        // Force this user's delivery day to today (whatever day the test happens to run) so the
        // test is deterministic regardless of the actual calendar date.
        var today = (int)DateTimeOffset.UtcNow.DayOfWeek;
        var pref = await _factory.Db.NotificationPreferences.FirstAsync(p => p.UserId == auth.UserId);
        pref.BusinessSummaryDayOfWeek = today;
        await _factory.Db.SaveChangesAsync();
        _factory.Db.ChangeTracker.Clear();

        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/internal/run-scheduled-jobs");
        req1.Headers.Add("X-Internal-Key", "test-internal-jobs-secret");
        var resp1 = await _factory.Client.SendAsync(req1);
        resp1.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result1 = await resp1.Content.ReadFromJsonAsync<ScheduledJobsRunResult>();
        result1!.BusinessSummariesSent.ShouldBeGreaterThanOrEqualTo(1);

        _factory.EmailSender.Sent.ShouldContain(e => e.ToEmail == auth.Email.ToLower());

        // LastBusinessSummarySentAt should now be set, so an immediate second run must not re-send.
        _factory.EmailSender.Clear();
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/internal/run-scheduled-jobs");
        req2.Headers.Add("X-Internal-Key", "test-internal-jobs-secret");
        var resp2 = await _factory.Client.SendAsync(req2);
        var result2 = await resp2.Content.ReadFromJsonAsync<ScheduledJobsRunResult>();
        result2!.BusinessSummariesSent.ShouldBe(0);
        _factory.EmailSender.Sent.ShouldNotContain(e => e.ToEmail == auth.Email.ToLower());
    }

    // ── Test 7 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task BusinessSummary_DisabledUser_NeverFires()
    {
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);

        var current = await _factory.Client.GetFromJsonAsync<NotificationPreferenceDto>(
            "/api/preferences/notifications", auth.Token);
        var disableReq = new UpdateNotificationPreferenceRequest(
            DailyDigestTime: current!.DailyDigestTime,
            DigestFrequencyDays: current.DigestFrequencyDays,
            DigestDaysOfWeek: current.DigestDaysOfWeek,
            SaleFollowUpDays: current.SaleFollowUpDays,
            DigestEnabled: current.DigestEnabled,
            StageChangeNotificationsEnabled: current.StageChangeNotificationsEnabled,
            SaleNotificationsEnabled: current.SaleNotificationsEnabled,
            BusinessSummaryEnabled: false
        );
        await _factory.Client.PutAsJsonAsync("/api/preferences/notifications", disableReq, auth.Token);

        var today = (int)DateTimeOffset.UtcNow.DayOfWeek;
        var pref = await _factory.Db.NotificationPreferences.FirstAsync(p => p.UserId == auth.UserId);
        pref.BusinessSummaryDayOfWeek = today;
        await _factory.Db.SaveChangesAsync();
        _factory.Db.ChangeTracker.Clear();

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/internal/run-scheduled-jobs");
        req.Headers.Add("X-Internal-Key", "test-internal-jobs-secret");
        var resp = await _factory.Client.SendAsync(req);
        var result = await resp.Content.ReadFromJsonAsync<ScheduledJobsRunResult>();

        result!.BusinessSummariesSent.ShouldBe(0);
        _factory.EmailSender.Sent.ShouldNotContain(e => e.ToEmail == auth.Email.ToLower());
    }
}
