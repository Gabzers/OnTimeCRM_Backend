using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Users;
using OnTimeCRM.Application.Interfaces;
using OnTimeCRM.Application.Interfaces.Repositories;

namespace OnTimeCRM.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repo;
    private readonly IUnitOfWork     _uow;

    public UserService(IUserRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow  = uow;
    }

    public async Task<UserDto> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _repo.FindWithBrandAndCompanyAsync(userId, ct)
            ?? throw new ApiException(ApiErrorCatalog.USER_NOT_FOUND);
        return ToDto(user);
    }

    public async Task<UserDto> UpdateMeAsync(
        Guid userId,
        UpdateUserRequest req,
        CancellationToken ct = default)
    {
        var user = await _repo.FindWithBrandAndCompanyAsync(userId, ct)
            ?? throw new ApiException(ApiErrorCatalog.USER_NOT_FOUND);

        if (req.FullName is not null) user.FullName = req.FullName;
        if (req.Phone    is not null) user.Phone    = req.Phone;

        await _uow.SaveChangesAsync(ct);
        return ToDto(user);
    }

    public Task<IEnumerable<UserListDto>> GetByBrandAsync(
        Guid brandId, CancellationToken ct = default) =>
        _repo.GetByBrandAsync(brandId, ct);

    public async Task<UserDto> GetByIdAsync(
        Guid userId, Guid brandId, CancellationToken ct = default)
    {
        var user = await _repo.FindInBrandAsync(userId, brandId, ct)
            ?? throw new ApiException(ApiErrorCatalog.USER_NOT_FOUND);
        return ToDto(user);
    }

    public async Task<UserDto> SetActiveAsync(
        Guid userId, Guid brandId, SetUserActiveRequest req, CancellationToken ct = default)
    {
        var user = await _repo.FindInBrandAsync(userId, brandId, ct)
            ?? throw new ApiException(ApiErrorCatalog.USER_NOT_FOUND);
        user.IsActive = req.IsActive;
        await _uow.SaveChangesAsync(ct);
        return ToDto(user);
    }

    private static UserDto ToDto(Domain.Entities.User u) =>
        new(u.Id, u.FullName, u.Email, u.Phone,
            (int)u.Role, (int)u.AccountStatus, 0,
            u.CompanyId, u.Company?.Name ?? string.Empty,
            u.BrandId,   u.Brand?.Name   ?? string.Empty,
            null, u.CreatedAt);
}
