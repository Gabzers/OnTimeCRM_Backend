using System.Net;
using System.Net.Http.Json;
using Shouldly;
using OnTime.Application.DTOs.Vehicles;
using OnTime.Tests.Infrastructure;
using PagedResult = OnTime.Application.Common.PagedResult<OnTime.Application.DTOs.Vehicles.VehicleModelListDto>;

namespace OnTime.Tests.Flows;

/// <summary>
/// Flow 10 — Vehicle Catalogue
/// Goal: Models default to active; managers can toggle active status; "configured" status
/// (at least one version with ≥1 exterior colour) is exposed for the Vehicles screen status dot.
/// </summary>
[Collection("Integration")]
public class VehicleFlowTests : IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public VehicleFlowTests(TestWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(string Token, Guid BrandId)> ArrangeManagerWithBrandAsync()
    {
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        var brandResp = await _factory.Client.PostAsJsonAsync(
            "/api/vehicles/brands", new CreateVehicleBrandRequest($"TestBrand-{Guid.NewGuid():N}", null), auth.Token);
        var brand = await brandResp.Content.ReadFromJsonAsync<VehicleBrandDto>();

        // Vehicle brands are now configured per-Stand by Manager/Admin (see USER-BRANDS.md) —
        // allow it on the manager's own Stand before any model can be created under it.
        await _factory.Client.PutAsJsonAsync(
            $"/api/brands/{auth.BrandId}/vehicle-brands",
            new OnTime.Application.DTOs.Brands.UpdateBrandVehicleBrandsRequest([brand!.Id]),
            auth.Token);

        return (auth.Token, brand!.Id);
    }

    // ── Test 1 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateModel_DefaultsToActive()
    {
        var (token, brandId) = await ArrangeManagerWithBrandAsync();

        var resp = await _factory.Client.PostAsJsonAsync(
            "/api/vehicles/models",
            new CreateVehicleModelRequest(brandId, "TestModel", null, null, null, null, null),
            token);

        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var model = await resp.Content.ReadFromJsonAsync<VehicleModelDto>();
        model!.IsActive.ShouldBeTrue();
    }

    // ── Test 2 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetModelActive_TogglesIsActive()
    {
        var (token, brandId) = await ArrangeManagerWithBrandAsync();
        var createResp = await _factory.Client.PostAsJsonAsync(
            "/api/vehicles/models",
            new CreateVehicleModelRequest(brandId, "TestModel", null, null, null, null, null),
            token);
        var model = await createResp.Content.ReadFromJsonAsync<VehicleModelDto>();

        // ACT — deactivate
        var patchResp = await _factory.Client.PatchAsJsonAsync(
            $"/api/vehicles/models/{model!.Id}/active",
            new SetVehicleModelActiveRequest(false), token);

        // ASSERT
        patchResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var listResp = await _factory.Client.GetAsync($"/api/vehicles/models?brandId={brandId}", token);
        var list = await listResp.Content.ReadFromJsonAsync<PagedResult>();
        list!.Items.First(m => m.Id == model.Id).IsActive.ShouldBeFalse();
    }

    // ── Test 3 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetModels_WithoutVersions_IsNotConfigured()
    {
        var (token, brandId) = await ArrangeManagerWithBrandAsync();
        await _factory.Client.PostAsJsonAsync(
            "/api/vehicles/models",
            new CreateVehicleModelRequest(brandId, "Unconfigured", null, null, null, null, null),
            token);

        var listResp = await _factory.Client.GetAsync($"/api/vehicles/models?brandId={brandId}", token);
        var list = await listResp.Content.ReadFromJsonAsync<PagedResult>();
        list!.Items.Single().IsConfigured.ShouldBeFalse();
    }

    // ── Test 4 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetModels_WithVersionWithExternalColor_IsConfigured()
    {
        var (token, brandId) = await ArrangeManagerWithBrandAsync();
        var modelResp = await _factory.Client.PostAsJsonAsync(
            "/api/vehicles/models",
            new CreateVehicleModelRequest(brandId, "Configured", null, null, null, null, null),
            token);
        var model = await modelResp.Content.ReadFromJsonAsync<VehicleModelDto>();

        await _factory.Client.PostAsJsonAsync(
            $"/api/vehicles/models/{model!.Id}/versions",
            new CreateVehicleVersionRequest("Base", ["Branco"], []),
            token);

        var listResp = await _factory.Client.GetAsync($"/api/vehicles/models?brandId={brandId}", token);
        var list = await listResp.Content.ReadFromJsonAsync<PagedResult>();
        list!.Items.Single().IsConfigured.ShouldBeTrue();
    }

    // ── Test 5 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetModels_FilterByConfigured_ReturnsOnlyMatchingStatus()
    {
        var (token, brandId) = await ArrangeManagerWithBrandAsync();

        var configuredResp = await _factory.Client.PostAsJsonAsync(
            "/api/vehicles/models",
            new CreateVehicleModelRequest(brandId, "Configured", null, null, null, null, null),
            token);
        var configuredModel = await configuredResp.Content.ReadFromJsonAsync<VehicleModelDto>();
        await _factory.Client.PostAsJsonAsync(
            $"/api/vehicles/models/{configuredModel!.Id}/versions",
            new CreateVehicleVersionRequest("Base", ["Branco"], []),
            token);

        await _factory.Client.PostAsJsonAsync(
            "/api/vehicles/models",
            new CreateVehicleModelRequest(brandId, "Unconfigured", null, null, null, null, null),
            token);

        var configuredOnly = await (await _factory.Client.GetAsync(
            $"/api/vehicles/models?brandId={brandId}&configured=true", token)).Content.ReadFromJsonAsync<PagedResult>();
        configuredOnly!.Items.Select(m => m.Name).ShouldBe(["Configured"]);

        var unconfiguredOnly = await (await _factory.Client.GetAsync(
            $"/api/vehicles/models?brandId={brandId}&configured=false", token)).Content.ReadFromJsonAsync<PagedResult>();
        unconfiguredOnly!.Items.Select(m => m.Name).ShouldBe(["Unconfigured"]);

        var both = await (await _factory.Client.GetAsync(
            $"/api/vehicles/models?brandId={brandId}", token)).Content.ReadFromJsonAsync<PagedResult>();
        both!.Items.Count().ShouldBe(2);
    }

}
