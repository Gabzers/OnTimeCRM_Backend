using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using OnTime.Application.DTOs.Brands;
using OnTime.Application.DTOs.Clients;
using OnTime.Application.DTOs.Proposals;
using OnTime.Domain.Entities;
using OnTime.Domain.Enums;
using OnTime.Tests.Infrastructure;
using PagedResult = OnTime.Application.Common.PagedResult<OnTime.Application.DTOs.Vehicles.VehicleModelListDto>;

namespace OnTime.Tests.Flows;

/// <summary>
/// Flow 11 — Stand-level vehicle catalog (redesigned 2026-06-27, twice in the same sprint).
/// Goal: which car brands a Stand sells is configured by Manager/Admin (BrandVehicleBrand), not
/// picked per-salesperson anymore. The personal catalog (UserVehicleModel/UserVehicleVersion)
/// stays per-user — cloned lazily the first time a user's Stand allows a brand they haven't
/// cloned yet. See USER-BRANDS.md.
/// </summary>
[Collection("Integration")]
public class BrandVehicleBrandsFlowTests : IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public BrandVehicleBrandsFlowTests(TestWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Configuring the Stand's vehicle brands ─────────────────────────────

    [Fact]
    public async Task GetVehicleBrands_NewStand_ReturnsEmptyList()
    {
        var manager = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, manager.UserId);

        var resp = await _factory.Client.GetAsync($"/api/brands/{manager.BrandId}/vehicle-brands", manager.Token);

        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<BrandVehicleBrandsDto>();
        dto!.VehicleBrandIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetVehicleBrands_PersistsSelection_AndIsReturnedOnGet()
    {
        var manager = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, manager.UserId);
        var brandA = await CreateGlobalBrandAsync(manager.Token);
        var brandB = await CreateGlobalBrandAsync(manager.Token);

        var putResp = await _factory.Client.PutAsJsonAsync(
            $"/api/brands/{manager.BrandId}/vehicle-brands",
            new UpdateBrandVehicleBrandsRequest([brandA, brandB]),
            manager.Token);
        putResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getResp = await _factory.Client.GetAsync($"/api/brands/{manager.BrandId}/vehicle-brands", manager.Token);
        var dto = await getResp.Content.ReadFromJsonAsync<BrandVehicleBrandsDto>();
        dto!.VehicleBrandIds.ShouldBe([brandA, brandB], ignoreOrder: true);
    }

    [Fact]
    public async Task SetVehicleBrands_OnAnotherCompanysBrand_IsForbidden()
    {
        var managerA = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, managerA.UserId);
        var managerB = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, managerB.UserId);

        var resp = await _factory.Client.PutAsJsonAsync(
            $"/api/brands/{managerB.BrandId}/vehicle-brands",
            new UpdateBrandVehicleBrandsRequest([]),
            managerA.Token);

        resp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── Allowing a brand clones the global catalog into each user's own ────

    [Fact]
    public async Task AllowingBrand_ClonesGlobalModelsIntoPersonalCatalog_OnFirstView()
    {
        var manager = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, manager.UserId);
        var brandA = await CreateGlobalBrandAsync(manager.Token);
        await SeedGlobalModelAsync(brandA, "Corolla", "Branco");

        await _factory.Client.PutAsJsonAsync(
            $"/api/brands/{manager.BrandId}/vehicle-brands",
            new UpdateBrandVehicleBrandsRequest([brandA]), manager.Token);

        // ACT — first time viewing the models list triggers the lazy clone
        var resp = await _factory.Client.GetAsync("/api/vehicles/models", manager.Token);
        var list = await resp.Content.ReadFromJsonAsync<PagedResult>();

        list!.Items.ShouldContain(m => m.Name == "Corolla" && m.BrandId == brandA);
        list.Items.Single(m => m.Name == "Corolla").IsConfigured.ShouldBeTrue();
    }

    [Fact]
    public async Task DisallowingBrand_HidesModelsForEveryUserOfTheStand_DoesNotDelete()
    {
        var manager = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, manager.UserId);
        var brandA = await CreateGlobalBrandAsync(manager.Token);
        await SeedGlobalModelAsync(brandA, "Corolla", "Branco");
        await _factory.Client.PutAsJsonAsync(
            $"/api/brands/{manager.BrandId}/vehicle-brands",
            new UpdateBrandVehicleBrandsRequest([brandA]), manager.Token);
        await _factory.Client.GetAsync("/api/vehicles/models", manager.Token); // triggers clone

        // ACT — Stand no longer sells brandA
        var putResp = await _factory.Client.PutAsJsonAsync(
            $"/api/brands/{manager.BrandId}/vehicle-brands",
            new UpdateBrandVehicleBrandsRequest([]), manager.Token);
        putResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var resp = await _factory.Client.GetAsync("/api/vehicles/models", manager.Token);
        var list = await resp.Content.ReadFromJsonAsync<PagedResult>();
        list!.Items.ShouldNotContain(m => m.Name == "Corolla");

        _factory.Db.ChangeTracker.Clear();
        var stillThere = await _factory.Db.UserVehicleModels
            .AnyAsync(m => m.UserId == manager.UserId && m.Name == "Corolla");
        stillThere.ShouldBeTrue();
    }

    [Fact]
    public async Task ReallowingBrand_ReusesClonedRows_WithoutDuplicating()
    {
        var manager = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, manager.UserId);
        var brandA = await CreateGlobalBrandAsync(manager.Token);
        await SeedGlobalModelAsync(brandA, "Corolla", "Branco");

        await _factory.Client.PutAsJsonAsync(
            $"/api/brands/{manager.BrandId}/vehicle-brands", new UpdateBrandVehicleBrandsRequest([brandA]), manager.Token);
        await _factory.Client.GetAsync("/api/vehicles/models", manager.Token);
        await _factory.Client.PutAsJsonAsync(
            $"/api/brands/{manager.BrandId}/vehicle-brands", new UpdateBrandVehicleBrandsRequest([]), manager.Token);

        // ACT — re-allow the same brand
        await _factory.Client.PutAsJsonAsync(
            $"/api/brands/{manager.BrandId}/vehicle-brands", new UpdateBrandVehicleBrandsRequest([brandA]), manager.Token);

        var resp = await _factory.Client.GetAsync("/api/vehicles/models", manager.Token);
        var list = await resp.Content.ReadFromJsonAsync<PagedResult>();
        list!.Items.Count(m => m.Name == "Corolla").ShouldBe(1);
    }

    // ── Manual model creation requires an allowed brand ─────────────────────

    [Fact]
    public async Task CreateModel_ForBrandNotAllowedByStand_IsRejected()
    {
        var manager = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, manager.UserId);
        var brandA = await CreateGlobalBrandAsync(manager.Token);

        var resp = await _factory.Client.PostAsJsonAsync(
            "/api/vehicles/models",
            new OnTime.Application.DTOs.Vehicles.CreateVehicleModelRequest(brandA, "Yaris", null, null, null, null, null),
            manager.Token);

        resp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var body = await resp.Content.ReadAsStringAsync();
        body.ShouldContain("VEHICLE_BRAND_NOT_ALLOWED");
    }

    [Fact]
    public async Task CreateModel_ForAllowedBrand_Succeeds()
    {
        var manager = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, manager.UserId);
        var brandA = await CreateGlobalBrandAsync(manager.Token);
        await _factory.Client.PutAsJsonAsync(
            $"/api/brands/{manager.BrandId}/vehicle-brands", new UpdateBrandVehicleBrandsRequest([brandA]), manager.Token);

        var resp = await _factory.Client.PostAsJsonAsync(
            "/api/vehicles/models",
            new OnTime.Application.DTOs.Vehicles.CreateVehicleModelRequest(brandA, "Yaris", null, null, null, null, null),
            manager.Token);

        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    // ── Tenant isolation across two different Stands ───────────────────────

    [Fact]
    public async Task TwoStands_AllowingSameBrand_GiveTheirUsersIndependentClones()
    {
        var manager1 = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, manager1.UserId);
        var manager2 = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, manager2.UserId);

        var brandA = await CreateGlobalBrandAsync(manager1.Token);
        await SeedGlobalModelAsync(brandA, "Corolla", "Branco");

        await _factory.Client.PutAsJsonAsync(
            $"/api/brands/{manager1.BrandId}/vehicle-brands", new UpdateBrandVehicleBrandsRequest([brandA]), manager1.Token);
        await _factory.Client.PutAsJsonAsync(
            $"/api/brands/{manager2.BrandId}/vehicle-brands", new UpdateBrandVehicleBrandsRequest([brandA]), manager2.Token);

        var resp1 = await _factory.Client.GetAsync("/api/vehicles/models", manager1.Token);
        var resp2 = await _factory.Client.GetAsync("/api/vehicles/models", manager2.Token);
        var model1 = (await resp1.Content.ReadFromJsonAsync<PagedResult>())!.Items.Single(m => m.Name == "Corolla");
        var model2 = (await resp2.Content.ReadFromJsonAsync<PagedResult>())!.Items.Single(m => m.Name == "Corolla");

        model1.Id.ShouldNotBe(model2.Id);

        var forbiddenGet = await _factory.Client.GetAsync($"/api/vehicles/models/{model1.Id}", manager2.Token);
        forbiddenGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── Delete is blocked when the model is in use ──────────────────────────

    [Fact]
    public async Task DeleteModel_WhenReferencedByProposal_IsBlocked()
    {
        var manager = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, manager.UserId);
        var brandA = await CreateGlobalBrandAsync(manager.Token);
        await _factory.Client.PutAsJsonAsync(
            $"/api/brands/{manager.BrandId}/vehicle-brands", new UpdateBrandVehicleBrandsRequest([brandA]), manager.Token);

        var createResp = await _factory.Client.PostAsJsonAsync(
            "/api/vehicles/models",
            new OnTime.Application.DTOs.Vehicles.CreateVehicleModelRequest(brandA, "Yaris", null, null, null, null, null),
            manager.Token);
        var model = await createResp.Content.ReadFromJsonAsync<OnTime.Application.DTOs.Vehicles.VehicleModelDto>();

        var stageId = await TestHelpers.GetFirstStageIdAsync(_factory.Client, manager.Token);
        var clientReq = new CreateClientRequest(
            FullName: "Cliente Teste",
            Email: "cliente.teste@example.com",
            Phone: "351911111111",
            TaxId: null,
            LeadSource: (int)LeadSource.WalkIn,
            StageId: stageId,
            Proposal: new CreateProposalReq(
                BusinessType: (int)BusinessType.DirectPurchase,
                PaymentType: (int)PaymentType.Financing,
                ProposalValue: 20000,
                ProposalDate: DateTimeOffset.UtcNow,
                Vehicles: [new ProposalVehicleRequest(model!.Id, null, true)]
            )
        );
        var clientResp = await _factory.Client.PostAsJsonAsync("/api/clients", clientReq, manager.Token);
        clientResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deleteResp = await _factory.Client.DeleteAsync($"/api/vehicles/models/{model.Id}", manager.Token);
        deleteResp.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await deleteResp.Content.ReadAsStringAsync();
        body.ShouldContain("VEHICLE_MODEL_IN_USE");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<Guid> CreateGlobalBrandAsync(string token)
    {
        var resp = await _factory.Client.PostAsJsonAsync(
            "/api/vehicles/brands",
            new OnTime.Application.DTOs.Vehicles.CreateVehicleBrandRequest($"Brand-{Guid.NewGuid():N}", null),
            token);
        var brand = await resp.Content.ReadFromJsonAsync<OnTime.Application.DTOs.Vehicles.VehicleBrandDto>();
        return brand!.Id;
    }

    /// <summary>Seeds a global VehicleModel+Version directly — there is no public endpoint for
    /// authoring the global "shelf" catalog; today it's populated out-of-band (seed/admin).</summary>
    private async Task SeedGlobalModelAsync(Guid brandId, string modelName, string externalColor)
    {
        var model = new VehicleModel { BrandId = brandId, Name = modelName };
        model.Versions.Add(new VehicleModelVersion { Name = "Base", ExternalColors = $"[\"{externalColor}\"]" });
        _factory.Db.VehicleModels.Add(model);
        await _factory.Db.SaveChangesAsync();
        _factory.Db.ChangeTracker.Clear();
    }
}
