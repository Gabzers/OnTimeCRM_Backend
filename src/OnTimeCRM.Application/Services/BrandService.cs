using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Brands;
using OnTimeCRM.Application.Interfaces;
using OnTimeCRM.Application.Interfaces.Repositories;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Application.Services;

public class BrandService : IBrandService
{
    private readonly IBrandRepository _repo;
    private readonly IAuthRepository  _authRepo;
    private readonly IUnitOfWork      _uow;

    public BrandService(IBrandRepository repo, IAuthRepository authRepo, IUnitOfWork uow)
    {
        _repo     = repo;
        _authRepo = authRepo;
        _uow      = uow;
    }

    public Task<IEnumerable<BrandListDto>> GetByCompanyAsync(
        Guid companyId, CancellationToken ct = default) =>
        _repo.GetByCompanyAsync(companyId, ct);

    public async Task<BrandDto> GetByIdAsync(
        Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await _repo.GetDtoByIdAsync(id, companyId, ct)
            ?? throw new ApiException(ApiErrorCatalog.BRAND_NOT_FOUND);
    }

    public async Task<BrandDto> CreateAsync(
        Guid companyId, CreateBrandRequest req, CancellationToken ct = default)
    {
        var company = await _authRepo.FindCompanyAsync(companyId, ct)
            ?? throw new ApiException(ApiErrorCatalog.COMPANY_NOT_FOUND);

        if (!company.IsActive)
            throw new ApiException(ApiErrorCatalog.COMPANY_INACTIVE);

        var brand = new Brand
        {
            CompanyId    = companyId,
            Name         = req.Name,
            Description  = req.Description,
            Phone        = req.Phone,
            Email        = req.Email,
            Address      = req.Address,
            LogoUrl      = req.LogoUrl,
            PrimaryColor = req.PrimaryColor ?? "#1677FF",
            IsActive     = true
        };

        _repo.Add(brand);
        await _uow.SaveChangesAsync(ct);
        return ToDto(brand);
    }

    public async Task<BrandDto> UpdateAsync(
        Guid id, Guid companyId, UpdateBrandRequest req, CancellationToken ct = default)
    {
        var brand = await _repo.FindAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.BRAND_NOT_FOUND);

        if (brand.CompanyId != companyId)
            throw new ApiException(ApiErrorCatalog.BRAND_WRONG_COMPANY);

        brand.Name         = req.Name;
        brand.Description  = req.Description;
        brand.Phone        = req.Phone;
        brand.Email        = req.Email;
        brand.Address      = req.Address;
        brand.LogoUrl      = req.LogoUrl;
        brand.PrimaryColor = req.PrimaryColor ?? brand.PrimaryColor;

        await _uow.SaveChangesAsync(ct);
        return ToDto(brand);
    }

    public async Task SetActiveAsync(
        Guid id, Guid companyId, bool isActive, CancellationToken ct = default)
    {
        var brand = await _repo.FindAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.BRAND_NOT_FOUND);

        if (brand.CompanyId != companyId)
            throw new ApiException(ApiErrorCatalog.BRAND_WRONG_COMPANY);

        brand.IsActive = isActive;
        await _uow.SaveChangesAsync(ct);
    }

    private static BrandDto ToDto(Brand b) =>
        new(b.Id, b.CompanyId, b.Name,
            b.Description, b.Phone, b.Email, b.Address, b.LogoUrl,
            b.PrimaryColor, b.IsActive, b.CreatedAt);
}
