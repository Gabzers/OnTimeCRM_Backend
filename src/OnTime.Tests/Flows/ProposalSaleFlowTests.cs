using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using OnTime.Application.DTOs.Clients;
using OnTime.Application.DTOs.Proposals;
using OnTime.Application.DTOs.Sales;
using OnTime.Domain.Enums;
using OnTime.Tests.Infrastructure;

namespace OnTime.Tests.Flows;

/// <summary>
/// Flow 4 — Proposals and Sale Conversion
/// Goal: Proposal → Sale conversion flow, SoldAt date handling, multiple sales per client.
/// </summary>
[Collection("Integration")]
public class ProposalSaleFlowTests : IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public ProposalSaleFlowTests(TestWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Test 1 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConvertProposalToSale_CreatesAllEntities_AndSchedulesPostSaleNotification()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (clientId, proposalId) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);

        var soldAt = DateTimeOffset.UtcNow.AddDays(-1); // sale happened yesterday
        var convertReq = new ConvertToSaleRequest(
            SoldAt: soldAt,
            FinalValue: 42500m,
            PaymentType: (int)PaymentType.Financing,
            ModelId: null,
            FreeTextModel: "BMW Serie 3 320d 2023",
            Plate: "AB-12-CD",
            Chassis: null,
            Obs: "First sale test"
        );

        // ACT
        var response = await _factory.Client.PostAsJsonAsync(
            $"/api/proposals/{proposalId}/convert", convertReq, auth.Token);

        // ASSERT — HTTP 200 + SaleDto
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sale = await response.Content.ReadFromJsonAsync<SaleDto>();
        sale.ShouldNotBeNull();
        sale!.FinalValue.ShouldBe(42500m);
        sale.Plate.ShouldBe("AB-12-CD");
        sale.FreeTextModel.ShouldBe("BMW Serie 3 320d 2023");

        // CRITICAL: SoldAt must match what was passed — NEVER UtcNow auto-set
        sale.SoldAt.Date.ShouldBe(soldAt.Date);

        // ASSERT — Proposal status = Won
        _factory.Db.ChangeTracker.Clear();
        var proposal = await _factory.Db.Proposals.FindAsync(proposalId);
        proposal!.Status.ShouldBe(ProposalStatus.Won);
        proposal.WonAt.ShouldNotBeNull();

        // ASSERT — Client moved to IsWon stage
        var client = await _factory.Db.Clients
            .Include(c => c.CurrentStage)
            .FirstAsync(c => c.Id == clientId);
        client.CurrentStage.IsWon.ShouldBeTrue();

        // ASSERT — Post-sale notification scheduled for 30 days after (default SaleFollowUpDays)
        var notification = await _factory.Db.Notifications
            .FirstOrDefaultAsync(n => n.ClientId == clientId && n.Trigger == NotificationTrigger.SaleClosed);
        notification.ShouldNotBeNull();
        notification!.ScheduledFor.Date.ShouldBe(DateTime.UtcNow.AddDays(30).Date);
    }

    [Fact]
    public async Task ConvertProposalToSale_UsesProposalVehicleWhenRequestOmitsVehicle()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        // ProposalVehicle.ModelId now points to the user's OWN catalog (UserVehicleModel), not
        // the global VehicleModel — configure the Manager's own Stand to sell this brand first
        // (Manager/Admin-only now, see USER-BRANDS.md), which lazily clones it into their catalog.
        var globalModel = await _factory.Db.VehicleModels
            .AsNoTracking()
            .Include(m => m.Brand)
            .FirstAsync(m => m.IsActive);

        await _factory.Client.PutAsJsonAsync(
            $"/api/brands/{auth.BrandId}/vehicle-brands",
            new OnTime.Application.DTOs.Brands.UpdateBrandVehicleBrandsRequest([globalModel.BrandId]),
            auth.Token);

        var myModels = await _factory.Client.GetFromJsonAsync<
            OnTime.Application.Common.PagedResult<OnTime.Application.DTOs.Vehicles.VehicleModelListDto>>(
            $"/api/vehicles/models?brandId={globalModel.BrandId}", auth.Token);
        var model = myModels!.Items.First(m => m.Name == globalModel.Name);

        var stageId = await TestHelpers.GetFirstStageIdAsync(_factory.Client, auth.Token);
        var createReq = new CreateClientRequest(
            FullName: "Model Backed Client",
            Email: "model-backed@example.com",
            Phone: "351912345678",
            TaxId: null,
            LeadSource: (int)LeadSource.WalkIn,
            StageId: stageId,
            Proposal: new CreateProposalReq(
                BusinessType: (int)BusinessType.DirectPurchase,
                PaymentType: (int)PaymentType.Cash,
                ProposalValue: 32000m,
                ProposalDate: DateTimeOffset.UtcNow,
                Vehicles: [new ProposalVehicleRequest(model.Id, null, true)]
            )
        );

        var createResp = await _factory.Client.PostAsJsonAsync("/api/clients", createReq, auth.Token);
        createResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var createdClient = await createResp.Content.ReadFromJsonAsync<ClientDto>();
        createdClient.ShouldNotBeNull();

        _factory.Db.ChangeTracker.Clear();
        var proposalId = await _factory.Db.Proposals
            .Where(p => p.ClientId == createdClient!.Id)
            .Select(p => p.Id)
            .FirstAsync();

        var convertReq = new ConvertToSaleRequest(
            SoldAt: DateTimeOffset.UtcNow.AddDays(-1),
            FinalValue: 32000m,
            PaymentType: (int)PaymentType.Cash,
            ModelId: null,
            FreeTextModel: null,
            Plate: null,
            Chassis: null,
            Obs: null
        );

        // ACT
        var response = await _factory.Client.PostAsJsonAsync(
            $"/api/proposals/{proposalId}/convert", convertReq, auth.Token);

        // ASSERT
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sale = await response.Content.ReadFromJsonAsync<SaleDto>();
        sale.ShouldNotBeNull();
        sale!.ModelId.ShouldBe(model.Id);
        sale.ModelName.ShouldBe($"{model.BrandName} {model.Name}");
    }

    // ── Test 2 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SoldAt_IsPreservedExactly_NotOverwrittenWithUtcNow()
    {
        // ARRANGE — Test the critical date rule: SoldAt must NOT be overridden
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (_, proposalId) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);

        // A specific date in the past
        var specificSoldAt = new DateTimeOffset(2024, 3, 15, 14, 30, 0, TimeSpan.Zero);

        var convertReq = new ConvertToSaleRequest(
            SoldAt: specificSoldAt,
            FinalValue: 30000m,
            PaymentType: (int)PaymentType.Cash,
            ModelId: null,
            FreeTextModel: "Test Vehicle",
            Plate: null,
            Chassis: null,
            Obs: null
        );

        // ACT
        var response = await _factory.Client.PostAsJsonAsync(
            $"/api/proposals/{proposalId}/convert", convertReq, auth.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sale = await response.Content.ReadFromJsonAsync<SaleDto>();

        // ASSERT — SoldAt is exactly as provided, confirming it was never auto-set to UtcNow
        sale!.SoldAt.Year.ShouldBe(2024);
        sale.SoldAt.Month.ShouldBe(3);
        sale.SoldAt.Day.ShouldBe(15);

        // Also verify in DB directly
        _factory.Db.ChangeTracker.Clear();
        var saleDb = await _factory.Db.Sales.FindAsync(sale.Id);
        saleDb!.SoldAt.Year.ShouldBe(2024);
        saleDb.SoldAt.Month.ShouldBe(3);
        saleDb.SoldAt.Day.ShouldBe(15);
    }

    // ── Test 3 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClientCanHaveMultipleSales_OverTime()
    {
        // ARRANGE — create client and convert first proposal
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (clientId, proposalId1) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);

        var firstSaleReq = new ConvertToSaleRequest(
            SoldAt: DateTimeOffset.UtcNow.AddMonths(-2),
            FinalValue: 20000m,
            PaymentType: (int)PaymentType.Cash,
            ModelId: null,
            FreeTextModel: "First Car",
            Plate: null, Chassis: null, Obs: null
        );
        var firstSaleResp = await _factory.Client.PostAsJsonAsync(
            $"/api/proposals/{proposalId1}/convert", firstSaleReq, auth.Token);
        firstSaleResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // ACT — create second proposal for same client and convert it too
        var secondProposalReq = new CreateProposalRequest(
            BusinessType: (int)BusinessType.DirectPurchase,
            PaymentType: (int)PaymentType.Financing,
            ProposalValue: 35000m,
            Discount: null,
            ProposalDate: DateTimeOffset.UtcNow,
            HasTradeIn: false,
            TradeInType: null, TradeInPlate: null, TradeInBrand: null, TradeInModel: null,
            TradeInYear: null, TradeInKm: null, TradeInEstimatedValue: null,
            Vehicles: [new OnTime.Application.DTOs.Clients.ProposalVehicleRequest(null, "Second Car", true)]
        );
        var proposal2Resp = await _factory.Client.PostAsJsonAsync(
            $"/api/clients/{clientId}/proposals", secondProposalReq, auth.Token);
        proposal2Resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var proposal2 = await proposal2Resp.Content.ReadFromJsonAsync<ProposalDto>();

        var secondSaleReq = new ConvertToSaleRequest(
            SoldAt: DateTimeOffset.UtcNow,
            FinalValue: 35000m,
            PaymentType: (int)PaymentType.Financing,
            ModelId: null, FreeTextModel: "Second Car",
            Plate: null, Chassis: null, Obs: null
        );
        var secondSaleResp = await _factory.Client.PostAsJsonAsync(
            $"/api/proposals/{proposal2!.Id}/convert", secondSaleReq, auth.Token);
        secondSaleResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // ASSERT — client sales history has 2 entries
        var salesResp = await _factory.Client.GetAsync($"/api/clients/{clientId}/sales", auth.Token);
        salesResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sales = await salesResp.Content.ReadFromJsonAsync<IEnumerable<ClientSaleHistoryDto>>();
        sales!.Count().ShouldBe(2);
    }

    // ── Test 4 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConvertAlreadyWonProposal_Returns422ProposalAlreadyClosed()
    {
        // ARRANGE — convert once
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (_, proposalId) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);

        var convertReq = new ConvertToSaleRequest(
            SoldAt: DateTimeOffset.UtcNow, FinalValue: 25000m,
            PaymentType: (int)PaymentType.Cash, ModelId: null, FreeTextModel: "Car",
            Plate: null, Chassis: null, Obs: null
        );
        var firstConvert = await _factory.Client.PostAsJsonAsync(
            $"/api/proposals/{proposalId}/convert", convertReq, auth.Token);
        firstConvert.StatusCode.ShouldBe(HttpStatusCode.OK);

        // ACT — try to convert again
        var secondConvert = await _factory.Client.PostAsJsonAsync(
            $"/api/proposals/{proposalId}/convert", convertReq, auth.Token);

        // ASSERT
        secondConvert.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await secondConvert.Content.ReadAsStringAsync();
        body.ShouldContain("PROPOSAL_ALREADY_CLOSED");
    }

    // ── Test 5 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConvertLostProposal_Returns422ProposalAlreadyClosed()
    {
        // ARRANGE — mark as lost first
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (_, proposalId) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);

        await _factory.Client.PostAsJsonAsync($"/api/proposals/{proposalId}/lost",
            new MarkProposalLostRequest((int)LossReason.Price, null), auth.Token);

        // ACT — try to convert
        var convertReq = new ConvertToSaleRequest(
            SoldAt: DateTimeOffset.UtcNow, FinalValue: 25000m,
            PaymentType: (int)PaymentType.Cash, ModelId: null, FreeTextModel: "Car",
            Plate: null, Chassis: null, Obs: null
        );
        var response = await _factory.Client.PostAsJsonAsync(
            $"/api/proposals/{proposalId}/convert", convertReq, auth.Token);

        // ASSERT
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("PROPOSAL_ALREADY_CLOSED");
    }

    // ── Test 6 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateClientWithProposal_WithNoVehicles_Returns422ProposalMissingVehicle()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        var req = new OnTime.Application.DTOs.Clients.CreateClientRequest(
            FullName: "Test Client",
            Email: "test@example.com",
            Proposal: new OnTime.Application.DTOs.Clients.CreateProposalReq(
                BusinessType: 0,
                PaymentType: 0,
                ProposalValue: 15000m,
                ProposalDate: DateTimeOffset.UtcNow,
                Vehicles: [] // empty list
            )
        );

        // ACT
        var resp = await _factory.Client.PostAsJsonAsync("/api/clients", req, auth.Token);

        // ASSERT — 422 with PROPOSAL_MISSING_VEHICLE
        resp.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await resp.Content.ReadAsStringAsync();
        body.ShouldContain("PROPOSAL_MISSING_VEHICLE");
    }

    [Fact]
    public async Task CreateProposalForClient_WithNoVehicles_Returns422ProposalMissingVehicle()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (clientId, _) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);

        var req = new CreateProposalRequest(
            BusinessType: 0,
            PaymentType: 0,
            ProposalValue: 10000m,
            Discount: null,
            ProposalDate: DateTimeOffset.UtcNow,
            HasTradeIn: false,
            TradeInType: null, TradeInPlate: null, TradeInBrand: null,
            TradeInModel: null, TradeInYear: null, TradeInKm: null,
            TradeInEstimatedValue: null,
            Vehicles: [] // empty
        );

        // ACT
        var resp = await _factory.Client.PostAsJsonAsync($"/api/clients/{clientId}/proposals", req, auth.Token);

        // ASSERT
        resp.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await resp.Content.ReadAsStringAsync();
        body.ShouldContain("PROPOSAL_MISSING_VEHICLE");
    }

    // ── Test 6b ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProposalForClient_WithPerVehicleDiscount_SumsIntoProposalDiscount()
    {
        // ARRANGE — "Nova Proposta" flow: no top-level Discount field, only per-vehicle discounts
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (clientId, _) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);

        var req = new CreateProposalRequest(
            BusinessType: 0, PaymentType: 0, ProposalValue: null, Discount: null,
            ProposalDate: DateTimeOffset.UtcNow, HasTradeIn: false,
            TradeInType: null, TradeInPlate: null, TradeInBrand: null,
            TradeInModel: null, TradeInYear: null, TradeInKm: null, TradeInEstimatedValue: null,
            Vehicles: [
                new ProposalVehicleRequest(null, "Car A", true, Price: 20000m, Discount: 500m),
                new ProposalVehicleRequest(null, "Car B", false, Price: 15000m, Discount: 300m),
            ]
        );

        // ACT
        var resp = await _factory.Client.PostAsJsonAsync($"/api/clients/{clientId}/proposals", req, auth.Token);

        // ASSERT — the proposal-level Discount is the sum of the vehicles' own discounts
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<ProposalDto>();
        dto!.Discount.ShouldBe(800m);
        dto.ProposalValue.ShouldBe(35000m);
    }

    // ── Test 6c ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPaged_ProposalWithOnlyFreeTextVehicle_ReturnsFreeTextAsVehicleName()
    {
        // ARRANGE — a proposal whose only vehicle has no catalog ModelId
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (clientId, _) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);

        var req = new CreateProposalRequest(
            BusinessType: 0, PaymentType: 0, ProposalValue: 12000m, Discount: null,
            ProposalDate: DateTimeOffset.UtcNow, HasTradeIn: false,
            TradeInType: null, TradeInPlate: null, TradeInBrand: null,
            TradeInModel: null, TradeInYear: null, TradeInKm: null, TradeInEstimatedValue: null,
            Vehicles: [new ProposalVehicleRequest(null, "Carro Usado Sem Catálogo", true)]
        );
        await _factory.Client.PostAsJsonAsync($"/api/clients/{clientId}/proposals", req, auth.Token);

        // ACT
        var resp = await _factory.Client.GetFromJsonAsync<PagedResult<ProposalListDto>>(
            $"/api/proposals?clientId={clientId}&pageSize=50", auth.Token);

        // ASSERT — the free-text proposal shows its own text, not a blank/whitespace value
        var item = resp!.Items.First(p => p.ProposalValue == 12000m);
        item.VehicleName.ShouldBe("Carro Usado Sem Catálogo");
    }

    // ── Test 6d ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConvertToSale_WithoutExplicitPlate_InheritsPlateFromTheProposalVehicle()
    {
        // ARRANGE — a proposal vehicle that already has a plate (used car)
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (clientId, _) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);

        var createReq = new CreateProposalRequest(
            BusinessType: 0, PaymentType: 0, ProposalValue: 9000m, Discount: null,
            ProposalDate: DateTimeOffset.UtcNow, HasTradeIn: false,
            TradeInType: null, TradeInPlate: null, TradeInBrand: null,
            TradeInModel: null, TradeInYear: null, TradeInKm: null, TradeInEstimatedValue: null,
            Vehicles: [new ProposalVehicleRequest(null, "Carro Usado", true, Plate: "AA-11-BB")]
        );
        var createResp = await _factory.Client.PostAsJsonAsync(
            $"/api/clients/{clientId}/proposals", createReq, auth.Token);
        var created = await createResp.Content.ReadFromJsonAsync<ProposalDto>();

        // ACT — convert to sale without specifying Plate in the request
        var convertReq = new ConvertToSaleRequest(SoldAt: DateTimeOffset.UtcNow, FinalValue: 9000m);
        var convertResp = await _factory.Client.PostAsJsonAsync(
            $"/api/proposals/{created!.Id}/convert", convertReq, auth.Token);

        // ASSERT
        convertResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sale = await convertResp.Content.ReadFromJsonAsync<SaleDto>();
        sale!.Plate.ShouldBe("AA-11-BB");
    }

    // ── Test 7 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkLost_WithLossReason_UpdatesProposalAndCreatesHistory()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (clientId, proposalId) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);

        var lostReq = new MarkProposalLostRequest(
            LossReason: (int)LossReason.Competition,
            LossNotes: "Client chose a competitor"
        );

        // ACT
        var response = await _factory.Client.PostAsJsonAsync(
            $"/api/proposals/{proposalId}/lost", lostReq, auth.Token);

        // ASSERT
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        _factory.Db.ChangeTracker.Clear();
        var proposal = await _factory.Db.Proposals.FindAsync(proposalId);
        proposal!.Status.ShouldBe(ProposalStatus.Lost);
        proposal.LossReason.ShouldBe(LossReason.Competition);
        proposal.LossNotes.ShouldBe("Client chose a competitor");
        proposal.LostAt.ShouldNotBeNull();

        // Stage history entry added for the lost state
        var history = await _factory.Db.ClientStageHistories
            .Where(h => h.ClientId == clientId)
            .ToListAsync();
        history.Count.ShouldBeGreaterThanOrEqualTo(2); // initial + lost stage change
    }
}
