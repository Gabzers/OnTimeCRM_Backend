using OnTimeCRM.Application.DTOs.Auth;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Application.Interfaces.Repositories;

/// <summary>
/// Data-access operations needed exclusively during registration and login.
/// Kept separate to make AuthService dependencies explicit and minimal.
/// </summary>
public interface IAuthRepository
{
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task<Company?> FindCompanyAsync(Guid id, CancellationToken ct = default);
    Task<Brand?> FindBrandAsync(Guid id, CancellationToken ct = default);
    Task<User?> FindByEmailWithNavigationsAsync(string email, CancellationToken ct = default);
    Task<IEnumerable<CompanyLookupDto>> GetCompanyListAsync(CancellationToken ct = default);
    Task<IEnumerable<BrandLookupDto>> GetBrandsByCompanyAsync(Guid companyId, CancellationToken ct = default);

    void AddCompany(Company company);
    void AddBrand(Brand brand);
    void AddUser(User user);
    void AddStage(ClientStage stage);
    void AddStageTemplate(StageNotificationTemplate template);
    void AddNotificationPreference(NotificationPreference pref);
    void AddPublicProfile(UserPublicProfile profile);
}
