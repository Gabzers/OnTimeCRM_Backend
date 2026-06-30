using System.Net;
using System.Net.Http.Json;
using Shouldly;
using OnTime.Application.DTOs.Auth;
using OnTime.Application.DTOs.Brands;
using OnTime.Domain.Enums;
using OnTime.Tests.Infrastructure;

namespace OnTime.Tests.Flows;

/// <summary>
/// Multi-Stand membership (2026-06-27): a user can belong to several companies/filiais.
/// User.CompanyId/BrandId stay the "currently active" Stand carried in the JWT — membership is
/// just the set of Stands a user is allowed to switch into. See 04-DECISIONS and USER-BRANDS.md.
/// </summary>
[Collection("Integration")]
public class MembershipFlowTests : IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public MembershipFlowTests(TestWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Registration_GrantsMembershipInTheCreatedStand()
    {
        var manager = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, manager.UserId);

        var resp = await _factory.Client.GetAsync("/api/users/me/memberships", manager.Token);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var memberships = await resp.Content.ReadFromJsonAsync<List<MembershipDto>>();

        memberships!.ShouldContain(m => m.BrandId == manager.BrandId);
    }

    [Fact]
    public async Task SwitchBrand_WithoutMembership_IsForbidden()
    {
        var manager1 = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, manager1.UserId);
        var manager2 = await TestHelpers.RegisterManagerAsync(_factory.Client);

        var resp = await _factory.Client.PostAsJsonAsync(
            "/api/users/me/switch-brand", new SwitchBrandRequest(manager2.BrandId!.Value), manager1.Token);

        resp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminGrantsMembership_ThenUserCanSwitchIntoIt()
    {
        var admin = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, admin.UserId);
        await PromoteToAdminAsync(admin.UserId);
        var adminToken = await TestHelpers.LoginAsync(_factory.Client, admin.Email);

        var otherManager = await TestHelpers.RegisterManagerAsync(_factory.Client);
        var salesperson = await TestHelpers.RegisterSalespersonAsync(
            _factory.Client, otherManager.CompanyId, otherManager.BrandId);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, salesperson.UserId);

        // A second Stand under a different company, that the salesperson does NOT belong to yet.
        var secondCompanyManager = await TestHelpers.RegisterManagerAsync(_factory.Client);

        var grantResp = await _factory.Client.PostAsJsonAsync(
            $"/api/admin/users/{salesperson.UserId}/memberships",
            new { BrandId = secondCompanyManager.BrandId }, adminToken);
        grantResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var switchResp = await _factory.Client.PostAsJsonAsync(
            "/api/users/me/switch-brand", new SwitchBrandRequest(secondCompanyManager.BrandId!.Value), salesperson.Token);
        switchResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var login = await switchResp.Content.ReadFromJsonAsync<LoginResponseDto>();
        login!.BrandId.ShouldBe(secondCompanyManager.BrandId);
        login.CompanyId.ShouldBe(secondCompanyManager.CompanyId);
    }

    [Fact]
    public async Task ManagerGrantsMembership_OnlyWithinOwnCompany()
    {
        var managerA = await TestHelpers.RegisterManagerAsync(_factory.Client);
        var managerB = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, managerB.UserId);
        var salespersonA = await TestHelpers.RegisterSalespersonAsync(
            _factory.Client, managerA.CompanyId, managerA.BrandId);

        // Manager B tries to grant membership into Manager B's OWN brand for a salesperson who
        // belongs to Manager A's company — allowed: granting membership doesn't require the
        // target user to already be in the company, only that the brand being granted is the
        // manager's own.
        var grantResp = await _factory.Client.PostAsJsonAsync(
            $"/api/brands/{managerB.BrandId}/members",
            new GrantMembershipRequest(salespersonA.UserId), managerB.Token);
        grantResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // But Manager B cannot grant membership into Manager A's brand (not their own company).
        var forbiddenResp = await _factory.Client.PostAsJsonAsync(
            $"/api/brands/{managerA.BrandId}/members",
            new GrantMembershipRequest(salespersonA.UserId), managerB.Token);
        forbiddenResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RevokeMembership_RemovesTheAbilityToSwitchIntoIt()
    {
        var manager = await TestHelpers.RegisterManagerAsync(_factory.Client);
        var salesperson = await TestHelpers.RegisterSalespersonAsync(
            _factory.Client, manager.CompanyId, manager.BrandId);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, salesperson.UserId);

        var secondManager = await TestHelpers.RegisterManagerAsync(_factory.Client);
        await TestHelpers.ActivateSubscriptionDirectAsync(_factory.Db, secondManager.UserId);
        await _factory.Client.PostAsJsonAsync(
            $"/api/brands/{secondManager.BrandId}/members",
            new GrantMembershipRequest(salesperson.UserId), secondManager.Token);

        var revokeResp = await _factory.Client.DeleteAsync(
            $"/api/brands/{secondManager.BrandId}/members/{salesperson.UserId}", secondManager.Token);
        revokeResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var switchResp = await _factory.Client.PostAsJsonAsync(
            "/api/users/me/switch-brand", new SwitchBrandRequest(secondManager.BrandId!.Value), salesperson.Token);
        switchResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task PromoteToAdminAsync(Guid userId)
    {
        var user = await _factory.Db.Users.FindAsync(userId);
        user!.Role = UserRole.Admin;
        await _factory.Db.SaveChangesAsync();
        _factory.Db.ChangeTracker.Clear();
    }
}
