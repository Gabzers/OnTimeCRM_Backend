using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using OnTime.Application.DTOs.Clients;
using OnTime.Application.DTOs.Proposals;
using OnTime.Application.DTOs.Sales;
using OnTime.Application.DTOs.Stages;
using OnTime.Application.DTOs.Vehicles;
using OnTime.Domain.Enums;
using OnTime.Tests.Infrastructure;

namespace OnTime.Tests.Flows;

/// <summary>
/// General Flow Tests — covers the full client pipeline from creation to sale/loss,
/// trade-in capture, dashboard KPIs, cascade deletes, and multi-tenant isolation.
/// </summary>
[Collection("Integration")]
public class GeneralFlowTests : IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public GeneralFlowTests(TestWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Test 1: Full client creation with all optional fields ────────────────

    [Fact]
    public async Task CreateClient_WithAllFields_CreatesClientProposalAndVehicles()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        // Create a vehicle brand + model via API (vehicle_brands is not seeded with models)
        var brandCreateResp = await _factory.Client.PostAsJsonAsync(
            "/api/vehicles/brands", new CreateVehicleBrandRequest($"TestBrand-{Guid.NewGuid():N}", null), auth.Token);
        var brandDto = await brandCreateResp.Content.ReadFromJsonAsync<VehicleBrandDto>();

        // Vehicle brands are configured per-Stand by Manager/Admin (see USER-BRANDS.md) — allow
        // it on the manager's own Stand before a model can be created under it.
        await _factory.Client.PutAsJsonAsync(
            $"/api/brands/{auth.BrandId}/vehicle-brands",
            new OnTime.Application.DTOs.Brands.UpdateBrandVehicleBrandsRequest([brandDto!.Id]),
            auth.Token);

        var modelCreateResp = await _factory.Client.PostAsJsonAsync(
            "/api/vehicles/models",
            new CreateVehicleModelRequest(brandDto!.Id, "TestModel", null, 2024, null, null, null),
            auth.Token);
        var modelDto = await modelCreateResp.Content.ReadFromJsonAsync<VehicleModelDto>();

        var req = new CreateClientRequest(
            FullName: "João Silva",
            Email: "joao@example.com",
            Phone: "351912345678",
            TaxId: "123456789",
            LeadSource: (int)LeadSource.Facebook,
            Notes: "Cliente muito interessado",
            Proposal: new CreateProposalReq(
                BusinessType: (int)BusinessType.DirectPurchase,
                PaymentType: (int)PaymentType.Financing,
                ProposalValue: 45000m,
                Discount: 1500m,
                ProposalDate: DateTimeOffset.UtcNow,
                Vehicles: [
                    new ProposalVehicleRequest(ModelId: modelDto!.Id, IsPreferred: true, Price: 45000m)
                ]
            )
        );

        // ACT
        var response = await _factory.Client.PostAsJsonAsync("/api/clients", req, auth.Token);

        // ASSERT
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ClientDto>();
        dto.ShouldNotBeNull();
        dto!.FullName.ShouldBe("João Silva");

        // Verify proposal was created atomically
        _factory.Db.ChangeTracker.Clear();
        var proposal = await _factory.Db.Proposals
            .Include(p => p.Vehicles)
            .FirstAsync(p => p.ClientId == dto.Id);
        proposal.ProposalValue.ShouldBe(45000m);
        proposal.Discount.ShouldBe(1500m);
        proposal.Vehicles.ShouldNotBeEmpty();
        proposal.Vehicles.First().ModelId.ShouldBe(modelDto!.Id);
    }

    // ── Test 2: Minimal client creation (only required fields) ──────────────

    [Fact]
    public async Task CreateClient_MinimalFields_Succeeds()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        var req = new CreateClientRequest(FullName: "Maria Santos");

        // ACT
        var response = await _factory.Client.PostAsJsonAsync("/api/clients", req, auth.Token);

        // ASSERT
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ClientDto>();
        dto.ShouldNotBeNull();
        dto!.FullName.ShouldBe("Maria Santos");

        // A proposal must always be created even with no proposal data
        _factory.Db.ChangeTracker.Clear();
        var exists = await _factory.Db.Proposals.AnyAsync(p => p.ClientId == dto.Id);
        exists.ShouldBeTrue();
    }

    // ── Test 3: Client creation with trade-in ────────────────────────────────

    [Fact]
    public async Task CreateClient_WithTradeIn_PersistsTradeInData()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        var req = new CreateClientRequest(
            FullName: "Carlos Oliveira",
            Proposal: new CreateProposalReq(
                BusinessType: (int)BusinessType.DirectPurchase,
                PaymentType: (int)PaymentType.Cash,
                HasTradeIn: true,
                TradeIn: new TradeInReq(
                    Plate: "AA-00-AA",
                    Brand: "Volkswagen",
                    Model: "Golf",
                    Year: 2019,
                    Km: 75000,
                    EstimatedValue: 12000m
                ),
                // At least one vehicle is required (proposal validation rule)
                Vehicles: [new ProposalVehicleRequest(null, "New Vehicle TBD", true)]
            )
        );

        // ACT
        var response = await _factory.Client.PostAsJsonAsync("/api/clients", req, auth.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ClientDto>();

        // ASSERT — Trade-in data persisted correctly
        _factory.Db.ChangeTracker.Clear();
        var proposal = await _factory.Db.Proposals.FirstAsync(p => p.ClientId == dto!.Id);
        proposal.HasTradeIn.ShouldBeTrue();
        proposal.TradeInPlate.ShouldBe("AA-00-AA");
        proposal.TradeInBrand.ShouldBe("Volkswagen");
        proposal.TradeInModel.ShouldBe("Golf");
        proposal.TradeInYear.ShouldBe(2019);
        proposal.TradeInKm.ShouldBe(75000);
        proposal.TradeInEstimatedValue.ShouldBe(12000m);
    }

    // ── Test 4: Full stage pipeline → mark as lost ───────────────────────────

    [Fact]
    public async Task StagePipeline_MoveClientThroughStages_ThenMarkLost()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (clientId, proposalId) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);

        var stages = await _factory.Client.GetFromJsonAsync<IEnumerable<ClientStageDto>>(
            "/api/stages", auth.Token);
        stages.ShouldNotBeNull();
        var orderedStages = stages!.OrderBy(s => s.Order).ToList();
        orderedStages.Count.ShouldBeGreaterThanOrEqualTo(3);

        // ACT — advance through stages 1, 2, 3
        foreach (var stage in orderedStages.Skip(1).Take(3))
        {
            var stageResp = await _factory.Client.PutAsJsonAsync(
                $"/api/clients/{clientId}/stage",
                new UpdateClientStageRequest(stage.Id, $"Moved to {stage.Name}"),
                auth.Token);
            stageResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Get current active proposal and mark as lost
        var lostStage = orderedStages.First(s => s.IsLost);
        var lostResp = await _factory.Client.PutAsJsonAsync(
            $"/api/clients/{clientId}/stage",
            new UpdateClientStageRequest(lostStage.Id, "Client decided not to buy"),
            auth.Token);
        lostResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Mark proposal lost with reason
        var lostProposalResp = await _factory.Client.PostAsJsonAsync(
            $"/api/proposals/{proposalId}/lost",
            new MarkProposalLostRequest(LossReason: (int)LossReason.Price, LossNotes: "Too expensive"),
            auth.Token);
        lostProposalResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // ASSERT
        _factory.Db.ChangeTracker.Clear();
        var proposal = await _factory.Db.Proposals.FindAsync(proposalId);
        proposal!.Status.ShouldBe(ProposalStatus.Lost);
        proposal.LossReason.ShouldBe(LossReason.Price);

        var client = await _factory.Db.Clients
            .Include(c => c.CurrentStage)
            .FirstAsync(c => c.Id == clientId);
        client.CurrentStage.IsLost.ShouldBeTrue();

        // Stage history should have multiple entries
        var history = await _factory.Db.ClientStageHistories
            .Where(h => h.ClientId == clientId)
            .ToListAsync();
        history.Count.ShouldBeGreaterThanOrEqualTo(4);
    }

    // ── Test 5: Full stage pipeline → convert to sale ────────────────────────

    [Fact]
    public async Task StagePipeline_MoveClientThroughStages_ThenConvertToSale()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (clientId, proposalId) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);

        var stages = await _factory.Client.GetFromJsonAsync<IEnumerable<ClientStageDto>>(
            "/api/stages", auth.Token);
        var orderedStages = stages!.OrderBy(s => s.Order).ToList();

        // ACT — advance a couple stages
        foreach (var stage in orderedStages.Skip(1).Take(2))
        {
            var r = await _factory.Client.PutAsJsonAsync(
                $"/api/clients/{clientId}/stage",
                new UpdateClientStageRequest(stage.Id, null),
                auth.Token);
            r.EnsureSuccessStatusCode();
        }

        // Convert proposal to sale — use a specific past date (NEVER UtcNow)
        var soldAt = DateTimeOffset.UtcNow.AddDays(-2);
        var convertResp = await _factory.Client.PostAsJsonAsync(
            $"/api/proposals/{proposalId}/convert",
            new ConvertToSaleRequest(
                SoldAt: soldAt,
                FinalValue: 38500m,
                PaymentType: (int)PaymentType.Financing,
                ModelId: null,
                FreeTextModel: "Voyah Free 2024",
                Plate: "VO-01-AH",
                Chassis: null,
                Obs: "First general flow sale"
            ),
            auth.Token);

        // ASSERT — 200 + sale DTO
        convertResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sale = await convertResp.Content.ReadFromJsonAsync<SaleDto>();
        sale.ShouldNotBeNull();
        sale!.FinalValue.ShouldBe(38500m);
        sale.SoldAt.Date.ShouldBe(soldAt.Date);

        // Client moved to Won stage
        _factory.Db.ChangeTracker.Clear();
        var client = await _factory.Db.Clients
            .Include(c => c.CurrentStage)
            .FirstAsync(c => c.Id == clientId);
        client.CurrentStage.IsWon.ShouldBeTrue();

        // Post-sale notification scheduled
        var notification = await _factory.Db.Notifications
            .FirstOrDefaultAsync(n => n.ClientId == clientId && n.Trigger == NotificationTrigger.SaleClosed);
        notification.ShouldNotBeNull();
    }

    // ── Test 6: Dashboard KPIs after sales ───────────────────────────────────

    [Fact]
    public async Task Dashboard_ReturnsCorrectKPIs_AfterTwoSalesThisMonth()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        // Create and convert 2 proposals to sales this month
        for (var i = 0; i < 2; i++)
        {
            var (_, proposalId) = await TestHelpers.CreateClientWithProposalAsync(
                _factory.Client, auth.Token, db: _factory.Db);

            await _factory.Client.PostAsJsonAsync(
                $"/api/proposals/{proposalId}/convert",
                new ConvertToSaleRequest(
                    SoldAt: DateTimeOffset.UtcNow,  // this month
                    FinalValue: 20000m + i * 5000m,
                    PaymentType: (int)PaymentType.Cash,
                    ModelId: null,
                    FreeTextModel: $"Dongfeng AX{i + 4}",
                    Plate: null,
                    Chassis: null,
                    Obs: null
                ),
                auth.Token);
        }

        // ACT
        var resp = await _factory.Client.GetAsync("/api/dashboard", auth.Token);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dashboard = await resp.Content.ReadFromJsonAsync<DashboardDto>();

        // ASSERT
        dashboard.ShouldNotBeNull();
        dashboard!.SalesThisMonth.ShouldBe(2);
        dashboard.RevenueThisMonth.ShouldBe(45000m); // 20000 + 25000
        dashboard.ProposalsThisMonth.ShouldBeGreaterThanOrEqualTo(2);
    }

    // ── Test 7: Delete brand cascades models ─────────────────────────────────

    [Fact]
    public async Task DeleteVehicleBrand_CascadesModelDeletion()
    {
        // ARRANGE — register as manager and create a new brand with models via API
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        // Create a new brand
        var brandResp = await _factory.Client.PostAsJsonAsync(
            "/api/vehicles/brands",
            new CreateVehicleBrandRequest($"DeleteBrand-{Guid.NewGuid():N}", null),
            auth.Token);
        brandResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var brand = await brandResp.Content.ReadFromJsonAsync<VehicleBrandDto>();
        brand.ShouldNotBeNull();

        // Allow it on the manager's own Stand first (see USER-BRANDS.md).
        await _factory.Client.PutAsJsonAsync(
            $"/api/brands/{auth.BrandId}/vehicle-brands",
            new OnTime.Application.DTOs.Brands.UpdateBrandVehicleBrandsRequest([brand.Id]),
            auth.Token);

        // Add a model to it
        await _factory.Client.PostAsJsonAsync(
            "/api/vehicles/models",
            new CreateVehicleModelRequest(brand!.Id, "TestModel", null, 2024, null, null, null),
            auth.Token);

        // ASSERT model count before — POST /models now creates a per-user UserVehicleModel
        _factory.Db.ChangeTracker.Clear();
        var countBefore = await _factory.Db.UserVehicleModels.CountAsync(m => m.VehicleBrandId == brand.Id);
        countBefore.ShouldBe(1);

        // ACT — delete the brand
        var deleteResp = await _factory.Client.DeleteAsync($"/api/vehicles/brands/{brand.Id}", auth.Token);
        deleteResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // ASSERT — personal models also deleted (cascade)
        _factory.Db.ChangeTracker.Clear();
        var countAfter = await _factory.Db.UserVehicleModels.CountAsync(m => m.VehicleBrandId == brand.Id);
        countAfter.ShouldBe(0);
        var brandExists = await _factory.Db.VehicleBrands.AnyAsync(b => b.Id == brand.Id);
        brandExists.ShouldBeFalse();
    }

    // ── Test 8: Tenant isolation ─────────────────────────────────────────────

    [Fact]
    public async Task TenantIsolation_UserBCannotSeeUserA_Clients()
    {
        // ARRANGE — two independent managers in different companies
        var authA = await TestHelpers.RegisterManagerAsync(_factory.Client, companyName: "Company A");
        var authB = await TestHelpers.RegisterManagerAsync(_factory.Client, companyName: "Company B");
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, authA.UserId);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, authB.UserId);

        // User A creates 3 clients
        for (var i = 0; i < 3; i++)
            await TestHelpers.CreateClientWithProposalAsync(_factory.Client, authA.Token, db: _factory.Db);

        // User B creates 1 client
        await TestHelpers.CreateClientWithProposalAsync(_factory.Client, authB.Token, db: _factory.Db);

        // ACT — User B lists clients
        var resp = await _factory.Client.GetFromJsonAsync<PagedResult<ClientListDto>>(
            "/api/clients", authB.Token);

        // ASSERT — User B sees only their 1 client, not User A's 3
        resp.ShouldNotBeNull();
        resp!.Items.Count().ShouldBe(1);

        // User B cannot see User A's clients by filtering nothing
        var totalInDb = await _factory.Db.Clients.CountAsync(c => c.IsActive);
        totalInDb.ShouldBe(4); // 4 total in DB, but User B only sees 1
    }
}
