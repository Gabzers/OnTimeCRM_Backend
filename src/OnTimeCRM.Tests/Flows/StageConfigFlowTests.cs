using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using OnTimeCRM.Application.DTOs.Clients;
using OnTimeCRM.Application.DTOs.Stages;
using OnTimeCRM.Domain.Enums;
using OnTimeCRM.Tests.Infrastructure;

namespace OnTimeCRM.Tests.Flows;

/// <summary>
/// Flow 8 — Stage Configuration
/// Goal: Stages are fully configurable; business rules (no delete with active clients) are enforced.
/// </summary>
[Collection("Integration")]
public class StageConfigFlowTests : IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public StageConfigFlowTests(TestWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Test 1 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterManager_Seeds7DefaultStages_WithCorrectProperties()
    {
        // ARRANGE + ACT
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        var resp = await _factory.Client.GetAsync("/api/stages", auth.Token);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stages = await resp.Content.ReadFromJsonAsync<IEnumerable<ClientStageDto>>();

        // ASSERT
        var stageList = stages!.OrderBy(s => s.Order).ToList();
        stageList.Count.ShouldBe(7);
        stageList[0].Name.ShouldBe("Aguarda Agendamento de Visita");
        stageList[0].IsFinal.ShouldBeFalse();
        stageList[5].Name.ShouldBe("Venda");
        stageList[5].IsFinal.ShouldBeTrue();
        stageList[5].IsWon.ShouldBeTrue();
        stageList[6].Name.ShouldBe("Perdido");
        stageList[6].IsFinal.ShouldBeTrue();
        stageList[6].IsLost.ShouldBeTrue();
    }

    // ── Test 2 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCustomStage_AppearsInList_WithCorrectOrder()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        var req = new CreateStageRequest(Name: "Negociação Avançada", Color: "#FF6B6B");

        // ACT
        var createResp = await _factory.Client.PostAsJsonAsync("/api/stages", req, auth.Token);
        createResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var newStage = await createResp.Content.ReadFromJsonAsync<ClientStageDto>();

        // ASSERT — appears in list
        var listResp = await _factory.Client.GetAsync("/api/stages", auth.Token);
        var stages = await listResp.Content.ReadFromJsonAsync<IEnumerable<ClientStageDto>>();
        stages!.ShouldContain(s => s.Id == newStage!.Id && s.Name == "Negociação Avançada");

        // ASSERT — in DB
        _factory.Db.ChangeTracker.Clear();
        var stageDb = await _factory.Db.ClientStages.FindAsync(newStage!.Id);
        stageDb.ShouldNotBeNull();
        stageDb!.Color.ShouldBe("#FF6B6B");
    }

    // ── Test 3 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReorderStages_UpdatesAllOrderValues()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        var stages = (await _factory.Client.GetFromJsonAsync<IEnumerable<ClientStageDto>>(
            "/api/stages", auth.Token))!.OrderBy(s => s.Order).ToList();

        // Reverse the order of the first 5 non-final stages
        var nonFinalStages = stages.Where(s => !s.IsFinal).OrderBy(s => s.Order).ToList();
        var reorderItems = nonFinalStages
            .Select((s, i) => new StageOrderItem(StageId: s.Id, Order: nonFinalStages.Count - 1 - i))
            .ToList();

        var reorderReq = new ReorderStagesRequest(Items: reorderItems);

        // ACT
        var resp = await _factory.Client.PostAsJsonAsync("/api/stages/reorder", reorderReq, auth.Token);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // ASSERT — order values updated in DB
        _factory.Db.ChangeTracker.Clear();
        foreach (var item in reorderItems)
        {
            var stageDb = await _factory.Db.ClientStages.FindAsync(item.StageId);
            stageDb!.Order.ShouldBe(item.Order);
        }
    }

    // ── Test 4 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteStage_WithActiveClients_Returns422StageHasClients()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        // Get the first non-final stage
        var stages = (await _factory.Client.GetFromJsonAsync<IEnumerable<ClientStageDto>>(
            "/api/stages", auth.Token))!.OrderBy(s => s.Order).ToList();
        var firstStage = stages.First(s => !s.IsFinal);

        // Create a client in that stage
        await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, stageId: firstStage.Id, db: _factory.Db);

        // ACT — attempt to delete the stage
        var resp = await _factory.Client.DeleteAsync($"/api/stages/{firstStage.Id}", auth.Token);

        // ASSERT — 422 Unprocessable Entity
        resp.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await resp.Content.ReadAsStringAsync();
        body.ShouldContain("STAGE_HAS_CLIENTS");
    }

    // ── Test 5 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteStage_WithoutClients_Succeeds()
    {
        // ARRANGE — create a custom stage (has no clients)
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        var createResp = await _factory.Client.PostAsJsonAsync("/api/stages",
            new CreateStageRequest(Name: "Deletable Stage", Color: "#AAAAAA"), auth.Token);
        var newStage = await createResp.Content.ReadFromJsonAsync<ClientStageDto>();

        // ACT
        var deleteResp = await _factory.Client.DeleteAsync($"/api/stages/{newStage!.Id}", auth.Token);

        // ASSERT
        deleteResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Stage should no longer appear in list
        var stages = await _factory.Client.GetFromJsonAsync<IEnumerable<ClientStageDto>>(
            "/api/stages", auth.Token);
        stages!.ShouldNotContain(s => s.Id == newStage.Id);
    }

    // ── Test 6 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddTemplateToStage_GeneratesNotificationWhenClientEntersStage()
    {
        // ARRANGE
        var auth = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, auth.UserId);

        var stages = (await _factory.Client.GetFromJsonAsync<IEnumerable<ClientStageDto>>(
            "/api/stages", auth.Token))!.OrderBy(s => s.Order).ToList();
        var targetStage = stages.First(s => !s.IsFinal && s.Order == 2); // "Agendar Test Drive"

        // Add template: notify 5 days after entering this stage
        var templateReq = new CreateStageTemplateRequest(Title: "Test Drive Follow-up", DaysAfter: 5);
        var templateResp = await _factory.Client.PostAsJsonAsync(
            $"/api/stages/{targetStage.Id}/templates", templateReq, auth.Token);
        templateResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Create client in first stage
        var (clientId, _) = await TestHelpers.CreateClientWithProposalAsync(
            _factory.Client, auth.Token, db: _factory.Db);

        // ACT — move client to the stage with the new template
        var stageChangeResp = await _factory.Client.PutAsJsonAsync(
            $"/api/clients/{clientId}/stage",
            new UpdateClientStageRequest(StageId: targetStage.Id, Obs: null), auth.Token);
        stageChangeResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // ASSERT — notification created with ScheduledFor = now + 5 days
        _factory.Db.ChangeTracker.Clear();
        var notification = await _factory.Db.Notifications
            .FirstOrDefaultAsync(n => n.ClientId == clientId && n.Trigger == NotificationTrigger.StageChanged);
        notification.ShouldNotBeNull();
        notification!.ScheduledFor.Date.ShouldBe(DateTime.UtcNow.AddDays(5).Date);
        notification.Title.ShouldBe("Test Drive Follow-up");
    }
}
