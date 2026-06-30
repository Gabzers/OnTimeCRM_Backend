using System.Net;
using System.Net.Http.Json;
using Shouldly;
using OnTime.Application.DTOs.Auth;
using OnTime.Application.DTOs.Brands;
using OnTime.Application.DTOs.Clients;
using OnTime.Application.DTOs.Proposals;
using OnTime.Domain.Enums;
using OnTime.Tests.Infrastructure;

namespace OnTime.Tests.Flows;

/// <summary>
/// "Not an automotive account" toggle (2026-06-29) — Brand.IsAutomotive, Manager/Admin-configured
/// per Stand. UI-hide only: when false, the backend also stops requiring at least one vehicle on
/// a new proposal (otherwise a non-automotive tenant could never create one through the normal
/// flow once the UI vehicle picker is hidden). See ROADMAP.md.
/// </summary>
[Collection("Integration")]
public class AutomotiveAccountFlowTests : IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public AutomotiveAccountFlowTests(TestWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task NewBrand_DefaultsToAutomotive()
    {
        var resp = await _factory.Client.PostAsJsonAsync("/api/auth/register-manager", new RegisterManagerRequest(
            FullName: "Test Manager", Email: $"{Guid.NewGuid():N}@example.com", Password: "Teste123!",
            CompanyName: "Test Co", BrandName: "Test Brand"));
        var login = await resp.Content.ReadFromJsonAsync<LoginResponseDto>();
        login!.IsAutomotive.ShouldBeTrue();
    }

    [Fact]
    public async Task Register_CanSetIsAutomotiveFalse_AtSignup()
    {
        var resp = await _factory.Client.PostAsJsonAsync("/api/auth/register-manager", new RegisterManagerRequest(
            FullName: "Test Manager", Email: $"{Guid.NewGuid():N}@example.com", Password: "Teste123!",
            CompanyName: "Test Co", BrandName: "Test Brand", IsAutomotive: false));
        var login = await resp.Content.ReadFromJsonAsync<LoginResponseDto>();
        login!.IsAutomotive.ShouldBeFalse();
    }

    [Fact]
    public async Task TogglingOff_ReflectsOnNextLogin()
    {
        var manager = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, manager.UserId);

        var brand = await GetBrandAsync(manager.Token, manager.BrandId!.Value);
        await _factory.Client.PutAsJsonAsync(
            $"/api/brands/{manager.BrandId}",
            new UpdateBrandRequest(brand.Name, brand.Description, brand.Phone, brand.Email,
                brand.Address, brand.LogoUrl, brand.PrimaryColor, IsAutomotive: false),
            manager.Token);

        var token = await TestHelpers.LoginAsync(_factory.Client, manager.Email);
        var resp = await _factory.Client.GetAsync("/api/users/me", token);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Re-login response itself already carried the new value — verify via a fresh login call.
        var loginResp = await _factory.Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(manager.Email, "Teste123!"));
        var login = await loginResp.Content.ReadFromJsonAsync<LoginResponseDto>();
        login!.IsAutomotive.ShouldBeFalse();
    }

    [Fact]
    public async Task NonAutomotive_CanCreateClientWithProposalWithoutAVehicle()
    {
        var manager = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, manager.UserId);
        await SetAutomotiveAsync(manager, isAutomotive: false);

        var stageId = await TestHelpers.GetFirstStageIdAsync(_factory.Client, manager.Token);
        var req = new CreateClientRequest(
            FullName: "Cliente Não-Automóvel",
            Email: "nao.automovel@example.com",
            Phone: "351911112222",
            TaxId: null,
            LeadSource: (int)LeadSource.WalkIn,
            StageId: stageId,
            Proposal: new CreateProposalReq(
                BusinessType: (int)BusinessType.DirectPurchase,
                PaymentType: (int)PaymentType.Cash,
                ProposalValue: 5000m,
                ProposalDate: DateTimeOffset.UtcNow,
                Vehicles: null
            )
        );

        var resp = await _factory.Client.PostAsJsonAsync("/api/clients", req, manager.Token);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AutomotiveAccount_StillRequiresAVehicle()
    {
        var manager = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, manager.UserId);

        var stageId = await TestHelpers.GetFirstStageIdAsync(_factory.Client, manager.Token);
        var req = new CreateClientRequest(
            FullName: "Cliente Automóvel",
            Email: "automovel@example.com",
            Phone: "351911113333",
            TaxId: null,
            LeadSource: (int)LeadSource.WalkIn,
            StageId: stageId,
            Proposal: new CreateProposalReq(
                BusinessType: (int)BusinessType.DirectPurchase,
                PaymentType: (int)PaymentType.Cash,
                ProposalValue: 5000m,
                ProposalDate: DateTimeOffset.UtcNow,
                Vehicles: null
            )
        );

        var resp = await _factory.Client.PostAsJsonAsync("/api/clients", req, manager.Token);
        resp.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await resp.Content.ReadAsStringAsync();
        body.ShouldContain("PROPOSAL_MISSING_VEHICLE");
    }

    [Fact]
    public async Task NonAutomotive_CanAddAStandaloneProposalWithoutAVehicle()
    {
        var manager = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, manager.UserId);
        await SetAutomotiveAsync(manager, isAutomotive: false);

        var (clientId, _) = await TestHelpers.CreateClientWithProposalAsync(_factory.Client, manager.Token, db: _factory.Db);

        var resp = await _factory.Client.PostAsJsonAsync(
            $"/api/clients/{clientId}/proposals",
            new CreateProposalRequest(
                BusinessType: (int)BusinessType.DirectPurchase,
                PaymentType: (int)PaymentType.Cash,
                ProposalValue: 3000m,
                Discount: null,
                ProposalDate: DateTimeOffset.UtcNow,
                HasTradeIn: false,
                TradeInType: null,
                TradeInPlate: null,
                TradeInBrand: null,
                TradeInModel: null,
                TradeInYear: null,
                TradeInKm: null,
                TradeInEstimatedValue: null,
                Vehicles: null
            ),
            manager.Token);

        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<BrandDto> GetBrandAsync(string token, Guid brandId)
    {
        var resp = await _factory.Client.GetAsync($"/api/brands/{brandId}", token);
        return (await resp.Content.ReadFromJsonAsync<BrandDto>())!;
    }

    private async Task SetAutomotiveAsync(AuthContext manager, bool isAutomotive)
    {
        var brand = await GetBrandAsync(manager.Token, manager.BrandId!.Value);
        var putResp = await _factory.Client.PutAsJsonAsync(
            $"/api/brands/{manager.BrandId}",
            new UpdateBrandRequest(brand.Name, brand.Description, brand.Phone, brand.Email,
                brand.Address, brand.LogoUrl, brand.PrimaryColor, isAutomotive),
            manager.Token);
        putResp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
