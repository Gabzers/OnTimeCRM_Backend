using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Companies;
using OnTimeCRM.Application.Interfaces;
using OnTimeCRM.Application.Interfaces.Repositories;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Application.Services;

public class AdminService : IAdminService
{
    private readonly IAdminRepository _repo;
    private readonly IUnitOfWork      _uow;

    public AdminService(IAdminRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow  = uow;
    }

    public Task<IEnumerable<CompanyAdminDto>> GetCompaniesAsync(
        CancellationToken ct = default) =>
        _repo.GetCompaniesAsync(ct);

    public async Task<CompanyAdminDto> CreateCompanyAsync(
        CreateCompanyAdminRequest request, CancellationToken ct = default)
    {
        var company = new Company
        {
            Name    = request.Name,
            Phone   = request.Phone,
            Email   = request.Email,
            Address = request.Address,
            IsActive = true
        };

        _repo.AddCompany(company);
        await _uow.SaveChangesAsync(ct);

        return new CompanyAdminDto(company.Id, company.Name, company.Phone,
            company.Email, company.Address, company.IsActive, 0, 0, company.CreatedAt);
    }

    public async Task<CompanyAdminDto> UpdateCompanyAsync(
        Guid id, UpdateCompanyAdminRequest request, CancellationToken ct = default)
    {
        var company = await _repo.FindCompanyAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.COMPANY_NOT_FOUND);

        company.Name    = request.Name;
        company.Phone   = request.Phone;
        company.Email   = request.Email;
        company.Address = request.Address;

        await _uow.SaveChangesAsync(ct);

        return new CompanyAdminDto(company.Id, company.Name, company.Phone,
            company.Email, company.Address, company.IsActive, 0, 0, company.CreatedAt);
    }

    public async Task SetCompanyActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var company = await _repo.FindCompanyAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.COMPANY_NOT_FOUND);

        company.IsActive = isActive;
        await _uow.SaveChangesAsync(ct);
    }
}
