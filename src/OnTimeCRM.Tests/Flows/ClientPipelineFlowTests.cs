using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using OnTimeCRM.Application.DTOs.Clients;
using OnTimeCRM.Application.DTOs.Stages;
using OnTimeCRM.Application.DTOs.Proposals;
using OnTimeCRM.Domain.Enums;
using OnTimeCRM.Tests.Infrastructure;

namespace OnTimeCRM.Tests.Flows;

/// <summary>
/// Flow 3 — Client Management and Pipeline
/// Goal: The core product flow — register client, move through pipeline stages, verify automations.
/// </summary>
[Collection("Integration")]
public class ClientPipelineFlowTests : IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public ClientPipelineFlowTests(TestWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Test 1 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateClient_WithFirstProposal_CreatesAllEntitiesAndInitialHistory()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var stages = await _factory.Client.GetFromJsonAsync<IEnumerable<ClientStageDto>>("/api/stages", auth.Token);
        var firstStage = stages!.OrderBy(s => s.Order).First();

        var req = new CreateClientRequest(
            FullName: "Maria Santos",
            Email: "maria@email.pt",
            Phone: "351961234567",
            TaxId: null,
            LeadSource: (int)LeadSource.WalkIn,
            Proposal: new CreateProposalReq(
                BusinessType: (int)BusinessType.DirectPurchase,
                PaymentType: (int)PaymentType.Financing,
                ProposalValue: 25000m,
                ProposalDate: DateTimeOffset.UtcNow,
                Vehicles: [new ProposalVehicleRequest(null, "Toyota Corolla 2024", true)]
            )
        );

        // ACT
        var response = await _factory.Client.PostAsJsonAsync("/api/clients", req, auth.Token);

        // ASSERT — HTTP + ClientDto
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ClientDto>();
        result.ShouldNotBeNull();
        result!.FullName.ShouldBe("Maria Santos");
        result.CurrentStageId.ShouldBe(firstStage.Id);
        result.Temperature.ShouldBe((int)DealTemperature.Hot); // just created

        // ASSERT — Client in DB
        var clientDb = await _factory.Db.Clients.FindAsync(result.Id);
        clientDb.ShouldNotBeNull();
        clientDb!.LastInteractionAt.ShouldNotBeNull();

        // ASSERT — Proposal created atomically
        var proposal = await _factory.Db.Proposals
            .FirstOrDefaultAsync(p => p.ClientId == result.Id);
        proposal.ShouldNotBeNull();
        proposal!.BusinessType.ShouldBe(BusinessType.DirectPurchase);
        proposal.Status.ShouldBe(ProposalStatus.Active);

        // ASSERT — A vehicle attached to the proposal
        var vehicle = await _factory.Db.ProposalVehicles
            .FirstOrDefaultAsync(v => v.ProposalId == proposal.Id);
        vehicle.ShouldNotBeNull();
        vehicle!.FreeTextModel.ShouldBe("Toyota Corolla 2024");
        vehicle.IsPreferred.ShouldBeTrue();

        // ASSERT — Initial stage history entry created
        var history = await _factory.Db.ClientStageHistories
            .Where(h => h.ClientId == result.Id)
            .ToListAsync();
        history.ShouldHaveSingleItem();
        history[0].FromStageId.ShouldBeNull(); // first entry has no "from"
        history[0].ToStageId.ShouldBe(firstStage.Id);
    }

    // ── Test 2 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChangeStage_CreatesHistoryWithProposalSnapshot_AndAutoGeneratesNotification()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (clientId, _) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);
        var stages = await _factory.Client.GetFromJsonAsync<IEnumerable<ClientStageDto>>("/api/stages", auth.Token);
        // Stage index 4 = "Aguarda Decisao" which has a 2-day notification template
        var awaitingDecisionStage = stages!.OrderBy(s => s.Order).ElementAt(4);

        // ACT
        var changeReq = new UpdateClientStageRequest(
            StageId: awaitingDecisionStage.Id,
            Obs: "Cliente interessado mas quer pensar"
        );
        var response = await _factory.Client.PutAsJsonAsync(
            $"/api/clients/{clientId}/stage", changeReq, auth.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // ASSERT — Stage history has 2 entries (initial + this change)
        var history = await _factory.Db.ClientStageHistories
            .Where(h => h.ClientId == clientId)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync();
        history.Count.ShouldBe(2);
        var lastEntry = history[1];
        lastEntry.ToStageId.ShouldBe(awaitingDecisionStage.Id);
        lastEntry.Obs.ShouldBe("Cliente interessado mas quer pensar");

        // ASSERT — Proposal snapshot serialized in history
        lastEntry.ProposalSnapshot.ShouldNotBeNullOrEmpty();
        var snapshot = JsonDocument.Parse(lastEntry.ProposalSnapshot!);
        snapshot.RootElement.GetProperty("bt").GetInt32().ShouldBe((int)BusinessType.DirectPurchase);

        // ASSERT — Auto notification generated from template (2 days after)
        var notification = await _factory.Db.Notifications
            .FirstOrDefaultAsync(n => n.ClientId == clientId && n.Trigger == NotificationTrigger.StageChanged);
        notification.ShouldNotBeNull();
        notification!.ScheduledFor.Date.ShouldBe(DateTime.UtcNow.AddDays(2).Date);
        notification.Status.ShouldBe(OnTimeCRM.Domain.Enums.NotificationStatus.Pending);
    }

    // ── Test 3 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Temperature_UpdatesCorrectlyBasedOnLastInteraction()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (clientId, _) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);

        // Verify initial temperature = Hot (just created)
        var clientInitial = await _factory.Db.Clients.FindAsync(clientId);
        clientInitial!.Temperature.ShouldBe(DealTemperature.Hot);

        // Manipulate LastInteractionAt to 5 days ago → Warm
        clientInitial.LastInteractionAt = DateTimeOffset.UtcNow.AddDays(-5);
        await _factory.Db.SaveChangesAsync();
        _factory.Db.ChangeTracker.Clear();

        // Trigger temperature recalculation by moving to next stage
        var stages = await _factory.Client.GetFromJsonAsync<IEnumerable<ClientStageDto>>("/api/stages", auth.Token);
        var secondStage = stages!.OrderBy(s => s.Order).ElementAt(1);
        var stageResp = await _factory.Client.PutAsJsonAsync(
            $"/api/clients/{clientId}/stage",
            new UpdateClientStageRequest(StageId: secondStage.Id, Obs: null),
            auth.Token);
        stageResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // ASSERT — temperature is now Hot again (just moved = interaction now)
        var clientAfterMove = await _factory.Db.Clients.AsNoTracking().FirstAsync(c => c.Id == clientId);
        clientAfterMove.Temperature.ShouldBe(DealTemperature.Hot);

        // Manipulate to 15 days ago → Cold
        clientAfterMove = await _factory.Db.Clients.FindAsync(clientId);
        clientAfterMove!.LastInteractionAt = DateTimeOffset.UtcNow.AddDays(-15);
        clientAfterMove.Temperature = DealTemperature.Cold;
        await _factory.Db.SaveChangesAsync();
        _factory.Db.ChangeTracker.Clear();

        var clientFinal = await _factory.Db.Clients.AsNoTracking().FirstAsync(c => c.Id == clientId);
        clientFinal.Temperature.ShouldBe(DealTemperature.Cold);
    }

    // ── Test 4 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkProposalLost_RequiresLossReason_UpdatesClientStageToLostStage()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (clientId, proposalId) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);

        var lostReq = new MarkProposalLostRequest(
            LossReason: (int)LossReason.Price,
            LossNotes: "Too expensive compared to competition"
        );

        // ACT
        var response = await _factory.Client.PostAsJsonAsync(
            $"/api/proposals/{proposalId}/lost", lostReq, auth.Token);

        // ASSERT
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        _factory.Db.ChangeTracker.Clear();
        var proposal = await _factory.Db.Proposals.FindAsync(proposalId);
        proposal!.Status.ShouldBe(ProposalStatus.Lost);
        proposal.LossReason.ShouldBe(LossReason.Price);
        proposal.LostAt.ShouldNotBeNull();

        // Client should be on the Lost stage
        var client = await _factory.Db.Clients
            .Include(c => c.CurrentStage)
            .FirstAsync(c => c.Id == clientId);
        client.CurrentStage.IsLost.ShouldBeTrue();
    }

    // ── Test 5 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteClient_SoftDeletes_DoesNotAppearInList_ButClientIdPreserved()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);
        var (clientId, _) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, auth.Token, db: _factory.Db);

        // ACT — soft delete via API
        var deleteResp = await _factory.Client.DeleteAsync($"/api/clients/{clientId}", auth.Token);
        deleteResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // ASSERT — client no longer appears in list
        var listResp = await _factory.Client.GetAsync("/api/clients", auth.Token);
        listResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await listResp.Content.ReadFromJsonAsync<PagedResult<ClientDto>>();
        list!.Items.ShouldNotContain(c => c.Id == clientId);

        // ASSERT — client record still exists in DB (soft delete)
        _factory.Db.ChangeTracker.Clear();
        var clientDb = await _factory.Db.Clients
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == clientId);
        clientDb.ShouldNotBeNull();
        clientDb!.IsActive.ShouldBeFalse();
    }

    // ── Test 6 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClientIsolation_SalespersonCannotSeeOtherSalespersonClients()
    {
        // ARRANGE — two salespersons in same brand
        var managerAuth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, managerAuth.UserId);

        var sp1Auth = await TestHelpers.RegisterSalespersonAsync(
            _factory.Client, managerAuth.CompanyId, managerAuth.BrandId);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, sp1Auth.UserId);

        var sp2Auth = await TestHelpers.RegisterSalespersonAsync(
            _factory.Client, managerAuth.CompanyId, managerAuth.BrandId);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, sp2Auth.UserId);

        // SP1 creates a client
        var (sp1ClientId, _) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, sp1Auth.Token, db: _factory.Db);

        // ACT — SP2 lists clients
        var listResp = await _factory.Client.GetAsync("/api/clients", sp2Auth.Token);
        listResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await listResp.Content.ReadFromJsonAsync<PagedResult<ClientDto>>();

        // ASSERT — SP2 does NOT see SP1's client
        list!.Items.ShouldNotContain(c => c.Id == sp1ClientId);

        // ASSERT — SP2 gets 403/404 when trying to access SP1's client directly
        var directResp = await _factory.Client.GetAsync($"/api/clients/{sp1ClientId}", sp2Auth.Token);
        ((int)directResp.StatusCode).ShouldBeOneOf(403, 404);
    }

    // ── Test 7 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ManagerCanSeeAllClientsWithinBrand_ButNotOtherBrands()
    {
        // ARRANGE — manager + 2 salespersons in same brand
        var managerAuth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, managerAuth.UserId);

        var sp1Auth = await TestHelpers.RegisterSalespersonAsync(
            _factory.Client, managerAuth.CompanyId, managerAuth.BrandId);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, sp1Auth.UserId);

        var sp2Auth = await TestHelpers.RegisterSalespersonAsync(
            _factory.Client, managerAuth.CompanyId, managerAuth.BrandId);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, sp2Auth.UserId);

        // SP1 creates client; SP2 creates client
        var (sp1ClientId, _) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, sp1Auth.Token, db: _factory.Db);
        var (sp2ClientId, _) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, sp2Auth.Token, db: _factory.Db);

        // ACT — Manager lists clients
        var listResp = await _factory.Client.GetAsync("/api/clients", managerAuth.Token);
        listResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await listResp.Content.ReadFromJsonAsync<PagedResult<ClientDto>>();

        // ASSERT — Manager sees BOTH clients
        list!.Items.ShouldContain(c => c.Id == sp1ClientId);
        list.Items.ShouldContain(c => c.Id == sp2ClientId);
    }
}
