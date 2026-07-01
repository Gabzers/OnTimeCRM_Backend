using OnTime.Application.Common;
using OnTime.Application.DTOs.Users;
using OnTime.Application.Interfaces;
using OnTime.Application.Interfaces.Repositories;

namespace OnTime.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repo;
    private readonly IUnitOfWork     _uow;
    private readonly IPasswordHasher _hasher;

    public UserService(IUserRepository repo, IUnitOfWork uow, IPasswordHasher hasher)
    {
        _repo   = repo;
        _uow    = uow;
        _hasher = hasher;
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
        if (req.Locale   is not null) user.Locale   = req.Locale;

        if (req.Email is not null && !req.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var email = req.Email.ToLower();
            if (await _repo.EmailTakenByAnotherUserAsync(email, userId, ct))
                throw new ApiException(ApiErrorCatalog.USER_EMAIL_TAKEN);
            user.Email = email;
        }

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

    public async Task ChangePasswordAsync(
        Guid userId, ChangePasswordRequest req, CancellationToken ct = default)
    {
        var user = await _repo.FindAsync(userId, ct)
            ?? throw new ApiException(ApiErrorCatalog.USER_NOT_FOUND);

        if (!_hasher.Verify(user.PasswordHash, req.CurrentPassword))
            throw new ApiException(ApiErrorCatalog.USER_CURRENT_PASSWORD_INVALID);

        user.PasswordHash = _hasher.Hash(req.NewPassword);
        await _uow.SaveChangesAsync(ct);
    }

    private static UserDto ToDto(Domain.Entities.User u) =>
        new(u.Id, u.FullName, u.Email, u.Phone,
            (int)u.Role, (int)u.AccountStatus, (int)u.SubscriptionStatus,
            u.CompanyId, u.Company?.Name ?? string.Empty,
            u.BrandId,   u.Brand?.Name   ?? string.Empty,
            u.LastLoginAt, u.CreatedAt, u.Locale);
}
