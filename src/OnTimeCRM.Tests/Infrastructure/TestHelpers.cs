using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bogus;
using Microsoft.EntityFrameworkCore;
using OnTimeCRM.Application.DTOs.Auth;
using OnTimeCRM.Application.DTOs.Clients;
using OnTimeCRM.Application.DTOs.Proposals;
using OnTimeCRM.Application.DTOs.Stages;
using OnTimeCRM.Domain.Enums;
using OnTimeCRM.Infrastructure.Persistence;

namespace OnTimeCRM.Tests.Infrastructure;

/// <summary>
/// Reusable helper methods that orchestrate common multi-step flows
/// (register, login, create client, activate subscription, etc.)
/// </summary>
public static class TestHelpers
{
    private static readonly Faker _faker = new Faker("pt_PT");

    // ── Auth helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a new Manager (creates Company + Brand + User) and returns an AuthContext
    /// with the JWT token and all relevant IDs.
    /// </summary>
    public static async Task<AuthContext> RegisterManagerAsync(
        HttpClient client,
        string? email = null,
        string? companyName = null,
        string? brandName = null)
    {
        email ??= _faker.Internet.Email();
        companyName ??= _faker.Company.CompanyName();
        brandName ??= $"Stand {_faker.Address.City()}";

        var req = new RegisterManagerRequest(
            FullName: _faker.Name.FullName(),
            Email: email,
            Password: "Teste123!",
            Phone: $"351{_faker.Random.Long(910000000, 999999999)}",
            CompanyName: companyName,
            BrandName: brandName,
            BrandColor: null
        );

        var response = await client.PostAsJsonAsync("/api/auth/register-manager", req);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        return new AuthContext(
            UserId: result!.UserId,
            CompanyId: result.CompanyId,
            BrandId: result.BrandId,
            Token: result.Token,
            Email: email
        );
    }

    /// <summary>
    /// Registers a Salesperson in an existing Brand and returns an AuthContext.
    /// </summary>
    public static async Task<AuthContext> RegisterSalespersonAsync(
        HttpClient client,
        Guid? companyId,
        Guid? brandId,
        string? email = null)
    {
        email ??= _faker.Internet.Email();

        var req = new RegisterSalespersonRequest(
            FullName: _faker.Name.FullName(),
            Email: email,
            Password: "Teste123!",
            Phone: $"351{_faker.Random.Long(910000000, 999999999)}",
            CompanyId: companyId,
            BrandId: brandId
        );

        var response = await client.PostAsJsonAsync("/api/auth/register", req);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        return new AuthContext(
            UserId: result!.UserId,
            CompanyId: result.CompanyId,
            BrandId: result.BrandId,
            Token: result.Token,
            Email: email
        );
    }

    /// <summary>
    /// Logs in with email/password and returns the JWT token.
    /// </summary>
    public static async Task<string> LoginAsync(HttpClient client, string email, string password = "Teste123!")
    {
        var req = new LoginRequest(Email: email, Password: password);
        var response = await client.PostAsJsonAsync("/api/auth/login", req);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        return result!.Token;
    }

    /// <summary>
    /// Gets the first (lowest-order) stage for the given user's auth token.
    /// </summary>
    public static async Task<Guid> GetFirstStageIdAsync(HttpClient client, string token)
    {
        var stages = await client.GetFromJsonAsync<IEnumerable<ClientStageDto>>(
            "/api/stages", token);
        return stages!.OrderBy(s => s.Order).First().Id;
    }

    /// <summary>
    /// Creates a client (with inline first proposal) in the given stage.
    /// Returns (clientId, proposalId) from the DB — looks up the proposal created.
    /// </summary>
    public static async Task<(Guid ClientId, Guid ProposalId)> CreateClientWithProposalAsync(
        HttpClient client,
        string token,
        Guid? stageId = null,
        AppDbContext? db = null)
    {
        if (stageId is null)
            stageId = await GetFirstStageIdAsync(client, token);

        var req = new CreateClientRequest(
            FullName: _faker.Name.FullName(),
            Email: _faker.Internet.Email(),
            Phone: $"351{_faker.Random.Long(910000000, 999999999)}",
            TaxId: null,
            LeadSource: (int)LeadSource.WalkIn,
            Proposal: new CreateProposalReq(
                BusinessType: (int)BusinessType.DirectPurchase,
                PaymentType: (int)PaymentType.Financing,
                ProposalValue: (decimal)_faker.Random.Int(15000, 60000),
                ProposalDate: DateTimeOffset.UtcNow,
                Vehicles: [new ProposalVehicleRequest(null, $"{_faker.Vehicle.Manufacturer()} {_faker.Vehicle.Model()}", true)]
            )
        );

        var response = await client.PostAsJsonAsync("/api/clients", req, token);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ClientDto>();
        var clientId = result!.Id;

        // Fetch the proposal ID — the API creates it atomically
        Guid proposalId;
        if (db != null)
        {
            db.ChangeTracker.Clear();
            proposalId = await db.Proposals
                .Where(p => p.ClientId == clientId)
                .Select(p => p.Id)
                .FirstAsync();
        }
        else
        {
            // Fetch via API
            var proposals = await client.GetFromJsonAsync<PagedResult<ProposalDto>>(
                $"/api/proposals?pageSize=1", token);
            proposalId = proposals!.Items.First().Id;
        }

        return (clientId, proposalId);
    }

    // ── Subscription helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Directly activates a user's subscription by manipulating the DB (bypasses payment flow).
    /// Use this in tests that don't test billing but need an active account.
    /// </summary>
    public static async Task ActivateSubscriptionDirectAsync(
        AppDbContext db,
        Guid userId,
        int days = 30)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null) throw new InvalidOperationException($"User {userId} not found.");

        var now = DateTimeOffset.UtcNow;
        user.AccountStatus = days > 0 ? UserAccountStatus.Active : UserAccountStatus.Expired;
        user.SubscriptionStatus = days > 0 ? SubscriptionStatus.Active : SubscriptionStatus.Expired;
        user.SubscriptionStartedAt = now.AddDays(-1);
        user.SubscriptionExpiresAt = now.AddDays(days);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    // ── Fake data builders ──────────────────────────────────────────────────

    /// <summary>Returns a Bogus faker configured for realistic Portuguese client data.</summary>
    public static Faker<CreateClientRequest> ClientFaker() => new Faker<CreateClientRequest>("pt_PT")
        .CustomInstantiator(f => new CreateClientRequest(
            FullName: f.Name.FullName(),
            Email: f.Internet.Email(),
            Phone: $"351{f.Random.Long(910000000, 999999999)}",
            TaxId: null,
            LeadSource: f.PickRandom<LeadSource>().GetHashCode(),
            Proposal: new CreateProposalReq(
                BusinessType: (int)BusinessType.DirectPurchase,
                PaymentType: (int)PaymentType.Cash,
                ProposalValue: (decimal)f.Random.Int(8000, 80000),
                ProposalDate: DateTimeOffset.UtcNow,
                Vehicles: [new ProposalVehicleRequest(null, $"{f.Vehicle.Manufacturer()} {f.Vehicle.Model()}", true)]
            )
        ));
}

/// <summary>
/// Holds authentication context (token + IDs) returned after login/register.
/// </summary>
public record AuthContext(
    Guid UserId,
    Guid? CompanyId,
    Guid? BrandId,
    string Token,
    string Email
);

/// <summary>
/// Minimal paged result shape to deserialize paginated API responses.
/// </summary>
public record PagedResult<T>(IEnumerable<T> Items, int TotalCount, int Page, int PageSize);

// ── Extension helpers for HttpClient ────────────────────────────────────────

public static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> PostAsJsonAsync<T>(
        this HttpClient client, string url, T body, string token) where T : notnull
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> PutAsJsonAsync<T>(
        this HttpClient client, string url, T body, string token) where T : notnull
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> PatchAsJsonAsync<T>(
        this HttpClient client, string url, T? body, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, url);
        if (body is not null) request.Content = JsonContent.Create(body);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> GetAsync(
        this HttpClient client, string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> DeleteAsync(
        this HttpClient client, string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    public static async Task<T?> GetFromJsonAsync<T>(
        this HttpClient client, string url, string token)
    {
        var response = await client.GetAsync(url, token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
