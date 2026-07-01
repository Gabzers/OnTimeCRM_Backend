using System.Net;
using System.Net.Http.Json;
using Shouldly;
using OnTime.Application.DTOs.LeadSources;
using OnTime.Tests.Infrastructure;

namespace OnTime.Tests.Flows;

/// <summary>
/// Lead Source maintenance — LeadSourceOption is per-user (not per-company).
/// Each registered user gets 6 default lead sources seeded on registration.
/// </summary>
[Collection("Integration")]
public class LeadSourceFlowTests : IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public LeadSourceFlowTests(TestWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task NewUser_GetsSixDefaultLeadSources()
    {
        var manager = await TestHelpers.RegisterManagerAsync(_factory.Client);

        var resp = await _factory.Client.GetAsync("/api/lead-sources", manager.Token);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var list = await resp.Content.ReadFromJsonAsync<List<LeadSourceOptionDto>>();
        list!.Count.ShouldBe(6);
        list.Select(x => x.Code).ShouldBe(Enumerable.Range(0, 6), ignoreOrder: true);
        list.ShouldAllBe(x => x.IsActive);
    }

    [Fact]
    public async Task AnyUser_CanCreateRenameAndDeactivate()
    {
        var manager = await TestHelpers.RegisterManagerAsync(_factory.Client);

        var createResp = await _factory.Client.PostAsJsonAsync(
            "/api/lead-sources", new CreateLeadSourceRequest("Google Ads"), manager.Token);
        createResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<LeadSourceOptionDto>();
        created!.Code.ShouldBe(6); // next after the 6 seeded defaults (0-5)

        var updateResp = await _factory.Client.PutAsJsonAsync(
            $"/api/lead-sources/{created.Id}", new UpdateLeadSourceRequest("Google Ads (Search)"), manager.Token);
        updateResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await updateResp.Content.ReadFromJsonAsync<LeadSourceOptionDto>();
        updated!.Name.ShouldBe("Google Ads (Search)");

        var deactivateResp = await _factory.Client.PatchAsJsonAsync(
            $"/api/lead-sources/{created.Id}/active", new SetLeadSourceActiveRequest(false), manager.Token);
        deactivateResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var listResp = await _factory.Client.GetAsync("/api/lead-sources", manager.Token);
        var list = await listResp.Content.ReadFromJsonAsync<List<LeadSourceOptionDto>>();
        list!.Single(x => x.Id == created.Id).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Salesperson_HasOwnLeadSourcesAndCanWrite()
    {
        var manager = await TestHelpers.RegisterManagerAsync(_factory.Client);
        var sales = await TestHelpers.RegisterSalespersonAsync(_factory.Client, manager.CompanyId!.Value, manager.BrandId!.Value);

        // Salesperson gets their own 6 defaults
        var listResp = await _factory.Client.GetAsync("/api/lead-sources", sales.Token);
        listResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await listResp.Content.ReadFromJsonAsync<List<LeadSourceOptionDto>>();
        list!.Count.ShouldBe(6);

        // Salesperson can create their own lead source
        var createResp = await _factory.Client.PostAsJsonAsync(
            "/api/lead-sources", new CreateLeadSourceRequest("OLX"), sales.Token);
        createResp.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task User_CannotEditAnotherUsersLeadSource()
    {
        var userA = await TestHelpers.RegisterManagerAsync(_factory.Client);
        var userB = await TestHelpers.RegisterManagerAsync(_factory.Client);

        var listResp = await _factory.Client.GetAsync("/api/lead-sources", userA.Token);
        var listA = await listResp.Content.ReadFromJsonAsync<List<LeadSourceOptionDto>>();
        var optionFromA = listA!.First();

        var resp = await _factory.Client.PutAsJsonAsync(
            $"/api/lead-sources/{optionFromA.Id}", new UpdateLeadSourceRequest("Hijacked"), userB.Token);
        resp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
