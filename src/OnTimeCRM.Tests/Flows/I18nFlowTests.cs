using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using OnTimeCRM.Tests.Infrastructure;

namespace OnTimeCRM.Tests.Flows;

/// <summary>
/// Flow 9 — i18n Translation System
/// Goal: Translation map is complete, versioned, and cacheable.
/// </summary>
[Collection("Integration")]
public class I18nFlowTests : IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public I18nFlowTests(TestWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Test 1 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTranslationMap_DefaultLocale_ReturnsAllRequiredKeys()
    {
        // ACT
        var resp = await _factory.Client.GetAsync("/api/i18n?locale=pt-PT");

        // ASSERT — HTTP 200
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await resp.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        // Version field present
        root.TryGetProperty("v", out var vProp).ShouldBeTrue();
        vProp.GetString().ShouldNotBeNullOrEmpty();

        // Map field present
        root.TryGetProperty("map", out var mapProp).ShouldBeTrue();

        // Required navigation keys
        mapProp.TryGetProperty("NAV.DASHBOARD", out _).ShouldBeTrue();
        mapProp.TryGetProperty("NAV.CLIENTS", out _).ShouldBeTrue();

        // Required enum keys
        mapProp.TryGetProperty("ENUM.LEAD_SOURCE.0", out _).ShouldBeTrue();
        mapProp.TryGetProperty("ENUM.DEAL_TEMPERATURE.0", out _).ShouldBeTrue();

        // Required account/subscription status keys
        mapProp.TryGetProperty("ACCOUNT.STATUS.0", out _).ShouldBeTrue();
        mapProp.TryGetProperty("SUBSCRIPTION.STATUS.0", out _).ShouldBeTrue();
    }

    // ── Test 2 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTranslationMap_ResponseIncludesVersionHeader()
    {
        // ACT
        var resp = await _factory.Client.GetAsync("/api/i18n?locale=pt-PT");

        // ASSERT — version available in response body
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.ShouldContain("\"v\"");
        // The X-I18n-Version header should also be present
        resp.Headers.TryGetValues("X-I18n-Version", out var versionHeader).ShouldBeTrue();
        versionHeader!.First().ShouldNotBeNullOrEmpty();
    }

    // ── Test 3 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTranslationMap_EnUS_ReturnsEnglishKeys()
    {
        // ACT — request English locale
        var resp = await _factory.Client.GetAsync("/api/i18n?locale=en-US");
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await resp.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        // locale field should reflect what was requested
        root.TryGetProperty("locale", out var localeProp).ShouldBeTrue();
        localeProp.GetString().ShouldBe("en-US");

        // Map must still have all keys
        root.TryGetProperty("map", out var mapProp).ShouldBeTrue();
        mapProp.TryGetProperty("NAV.DASHBOARD", out _).ShouldBeTrue();
    }

    // ── Test 4 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTranslationMap_UnknownLocale_FallsBackToPtPT()
    {
        // ACT — request a locale that doesn't exist
        var resp = await _factory.Client.GetAsync("/api/i18n?locale=xx-XX");

        // ASSERT — should still return 200 (falls back to pt-PT)
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await resp.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        json.RootElement.TryGetProperty("map", out _).ShouldBeTrue();
    }
}
