using Microsoft.EntityFrameworkCore;
using OnTimeCRM.Application.DTOs.Auth;
using OnTimeCRM.Application.Interfaces.Repositories;
using OnTimeCRM.Domain.Entities;
using OnTimeCRM.Infrastructure.Persistence;

namespace OnTimeCRM.Infrastructure.Repositories;

public sealed class AuthRepository : IAuthRepository
{
    private readonly AppDbContext _db;

    public AuthRepository(AppDbContext db) => _db = db;

    // ── Reads ─────────────────────────────────────────────────────────────

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) =>
        await _db.Users.AnyAsync(u => u.Email == email, ct);

    public async Task<Company?> FindCompanyAsync(Guid id, CancellationToken ct = default) =>
        await _db.Companies.FindAsync(new object[] { id }, ct);

    public async Task<Brand?> FindBrandAsync(Guid id, CancellationToken ct = default) =>
        await _db.Brands.FindAsync(new object[] { id }, ct);

    public async Task<User?> FindByEmailWithNavigationsAsync(
        string email,
        CancellationToken ct = default) =>
        await _db.Users
            .Include(u => u.Brand)
            .Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<IEnumerable<CompanyLookupDto>> GetCompanyListAsync(
        CancellationToken ct = default) =>
        await _db.Companies
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CompanyLookupDto(c.Id, c.Name))
            .ToListAsync(ct);

    public async Task<IEnumerable<BrandLookupDto>> GetBrandsByCompanyAsync(
        Guid companyId, CancellationToken ct = default) =>
        await _db.Brands
            .AsNoTracking()
            .Where(b => b.CompanyId == companyId && b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new BrandLookupDto(b.Id, b.Name, b.PrimaryColor))
            .ToListAsync(ct);

    // ── Writes ────────────────────────────────────────────────────────────

    public void AddCompany(Company company) => _db.Companies.Add(company);
    public void AddBrand(Brand brand) => _db.Brands.Add(brand);
    public void AddUser(User user) => _db.Users.Add(user);
    public void AddStage(ClientStage stage) => _db.ClientStages.Add(stage);
    public void AddStageTemplate(StageNotificationTemplate template) => _db.StageNotificationTemplates.Add(template);
    public void AddNotificationPreference(NotificationPreference pref) => _db.NotificationPreferences.Add(pref);
    public void AddPublicProfile(UserPublicProfile profile) => _db.UserPublicProfiles.Add(profile);
}
