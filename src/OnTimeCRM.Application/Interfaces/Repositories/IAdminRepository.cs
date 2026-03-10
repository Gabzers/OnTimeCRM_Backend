using OnTimeCRM.Application.DTOs.Companies;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Application.Interfaces.Repositories;

public interface IAdminRepository
{
    Task<IEnumerable<CompanyAdminDto>> GetCompaniesAsync(CancellationToken ct = default);
    Task<Company?> FindCompanyAsync(Guid id, CancellationToken ct = default);
    void AddCompany(Company company);
}
