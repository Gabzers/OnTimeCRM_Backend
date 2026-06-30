using OnTime.Application.Common;
using OnTime.Application.DTOs.Auth;
using OnTime.Application.DTOs.Users;
using OnTime.Application.Interfaces;
using OnTime.Application.Interfaces.Repositories;
using OnTime.Domain.Entities;
using OnTime.Domain.Enums;

namespace OnTime.Application.Services;

public class AuthService : IAuthService
{
    private readonly IAuthRepository _repo;
    private readonly IUnitOfWork     _uow;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtService     _jwt;

    public AuthService(
        IAuthRepository repo,
        IUnitOfWork uow,
        IPasswordHasher hasher,
        IJwtService jwt)
    {
        _repo   = repo;
        _uow    = uow;
        _hasher = hasher;
        _jwt    = jwt;
    }

    public async Task<LoginResponseDto> RegisterManagerAsync(
        RegisterManagerRequest req,
        CancellationToken ct = default)
    {
        if (await _repo.EmailExistsAsync(req.Email.ToLower(), ct))
            throw new ApiException(ApiErrorCatalog.USER_EMAIL_TAKEN);

        Company? company = null;
        Brand?   brand   = null;

        if (!string.IsNullOrWhiteSpace(req.CompanyName))
        {
            company = new Company { Name = req.CompanyName, IsActive = true };
            _repo.AddCompany(company);
            SeedDefaultLeadSources(company);

            if (!string.IsNullOrWhiteSpace(req.BrandName))
            {
                brand = new Brand
                {
                    Company      = company,
                    Name         = req.BrandName,
                    PrimaryColor = req.BrandColor ?? "#1677FF",
                    IsActive     = true,
                    IsAutomotive = req.IsAutomotive
                };
                _repo.AddBrand(brand);
            }
        }
        else if (req.CompanyId.HasValue)
        {
            company = await _repo.FindCompanyAsync(req.CompanyId.Value, ct)
                ?? throw new ApiException(ApiErrorCatalog.COMPANY_NOT_FOUND);

            if (req.BrandId.HasValue)
                brand = await _repo.FindBrandAsync(req.BrandId.Value, ct)
                    ?? throw new ApiException(ApiErrorCatalog.BRAND_NOT_FOUND);
        }

        var user = new User
        {
            Company            = company,
            Brand              = brand,
            Email              = req.Email.ToLower(),
            PasswordHash       = _hasher.Hash(req.Password),
            FullName           = req.FullName,
            Phone              = req.Phone,
            Role               = UserRole.Manager,
            AccountStatus      = UserAccountStatus.PendingActivation,
            SubscriptionStatus = SubscriptionStatus.Trial,
            TrialEndsAt        = DateTimeOffset.UtcNow.AddDays(14),
            IsActive           = true
        };
        _repo.AddUser(user);
        if (brand is not null) _repo.AddMembership(new UserBrandMembership { User = user, Brand = brand });

        SeedDefaultStages(user);
        SeedDefaultNotificationPreference(user);
        SeedDefaultPublicProfile(user);

        await _uow.SaveChangesAsync(ct);

        return BuildLoginResponse(user);
    }

    public async Task<LoginResponseDto> RegisterSalespersonAsync(
        RegisterSalespersonRequest req,
        CancellationToken ct = default)
    {
        if (await _repo.EmailExistsAsync(req.Email.ToLower(), ct))
            throw new ApiException(ApiErrorCatalog.USER_EMAIL_TAKEN);

        Company? company = null;
        Brand?   brand   = null;

        if (req.CompanyId.HasValue)
        {
            company = await _repo.FindCompanyAsync(req.CompanyId.Value, ct)
                ?? throw new ApiException(ApiErrorCatalog.COMPANY_NOT_FOUND);

            if (!company.IsActive)
                throw new ApiException(ApiErrorCatalog.COMPANY_INACTIVE);

            if (req.BrandId.HasValue)
            {
                brand = await _repo.FindBrandAsync(req.BrandId.Value, ct)
                    ?? throw new ApiException(ApiErrorCatalog.BRAND_NOT_FOUND);

                if (brand.CompanyId != req.CompanyId)
                    throw new ApiException(ApiErrorCatalog.BRAND_WRONG_COMPANY);

                if (!brand.IsActive)
                    throw new ApiException(ApiErrorCatalog.BRAND_INACTIVE);
            }
        }

        var user = new User
        {
            Company            = company,
            Brand              = brand,
            Email              = req.Email.ToLower(),
            PasswordHash       = _hasher.Hash(req.Password),
            FullName           = req.FullName,
            Phone              = req.Phone,
            Role               = UserRole.Salesperson,
            AccountStatus      = UserAccountStatus.PendingActivation,
            SubscriptionStatus = SubscriptionStatus.Trial,
            TrialEndsAt        = DateTimeOffset.UtcNow.AddDays(14),
            IsActive           = true
        };
        _repo.AddUser(user);
        if (brand is not null) _repo.AddMembership(new UserBrandMembership { User = user, Brand = brand });

        SeedDefaultStages(user);
        SeedDefaultNotificationPreference(user);
        SeedDefaultPublicProfile(user);

        await _uow.SaveChangesAsync(ct);

        return BuildLoginResponse(user);
    }

    public async Task<LoginResponseDto> LoginAsync(
        LoginRequest req,
        CancellationToken ct = default)
    {
        var user = await _repo.FindByEmailWithNavigationsAsync(req.Email.ToLower(), ct)
            ?? throw new ApiException(ApiErrorCatalog.USER_INVALID_CREDENTIALS);

        if (!_hasher.Verify(user.PasswordHash, req.Password))
            throw new ApiException(ApiErrorCatalog.USER_INVALID_CREDENTIALS);

        if (!user.IsActive)
            throw new ApiException(ApiErrorCatalog.USER_INACTIVE);

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _uow.SaveChangesAsync(ct);

        return BuildLoginResponse(user);
    }

    public Task<IEnumerable<MembershipDto>> GetMyMembershipsAsync(Guid userId, CancellationToken ct = default) =>
        _repo.GetMembershipsAsync(userId, ct);

    public async Task<LoginResponseDto> SwitchBrandAsync(Guid userId, Guid brandId, CancellationToken ct = default)
    {
        if (!await _repo.HasMembershipAsync(userId, brandId, ct))
            throw new ApiException(ApiErrorCatalog.AUTH_FORBIDDEN);

        var user = await _repo.FindByIdWithNavigationsAsync(userId, ct)
            ?? throw new ApiException(ApiErrorCatalog.USER_NOT_FOUND);
        var brand = await _repo.FindBrandAsync(brandId, ct)
            ?? throw new ApiException(ApiErrorCatalog.BRAND_NOT_FOUND);

        user.BrandId   = brand.Id;
        user.CompanyId = brand.CompanyId;
        user.Brand     = brand;
        user.Company   = await _repo.FindCompanyAsync(brand.CompanyId, ct);

        await _uow.SaveChangesAsync(ct);

        return BuildLoginResponse(user);
    }

    private LoginResponseDto BuildLoginResponse(User user)
    {
        var row = new UserDetailRow(
            Id:             user.Id,
            CompanyId:      user.CompanyId,
            BrandId:        user.BrandId,
            FullName:       user.FullName,
            Email:          user.Email,
            PasswordHash:   user.PasswordHash,
            Phone:          user.Phone,
            Role:           (int)user.Role,
            AccountStatus:  (int)user.AccountStatus,
            IsActive:       user.IsActive,
            CompanyName:    user.Company?.Name ?? string.Empty,
            CompanyIsActive:user.Company?.IsActive ?? true,
            BrandName:      user.Brand?.Name ?? string.Empty,
            BrandColor:     user.Brand?.PrimaryColor,
            BrandIsActive:  user.Brand?.IsActive ?? true);

        var token  = _jwt.GenerateToken(row);
        var expiry = _jwt.GetExpiry();

        return new LoginResponseDto(
            Token:                 token,
            ExpiresAt:             expiry,
            UserId:                user.Id,
            FullName:              user.FullName,
            Role:                  (int)user.Role,
            CompanyId:             user.CompanyId,
            CompanyName:           user.Company?.Name ?? string.Empty,
            BrandId:               user.BrandId,
            BrandName:             user.Brand?.Name ?? string.Empty,
            BrandColor:            user.Brand?.PrimaryColor,
            AccountStatus:         (int)user.AccountStatus,
            SubscriptionStatus:    (int)user.SubscriptionStatus,
            SubscriptionExpiresAt: user.SubscriptionExpiresAt,
            IsAutomotive:          user.Brand?.IsAutomotive ?? true);
    }

    private void SeedDefaultLeadSources(Company company)
    {
        var names = new[] { "Stand", "Telefone", "OLX", "Standvirtual", "Instagram", "Facebook", "Referência", "Outro" };
        for (var code = 0; code < names.Length; code++)
            _repo.AddLeadSource(new LeadSourceOption { Company = company, Code = code, Name = names[code] });
    }

    private void SeedDefaultStages(User user)
    {
        var stages = new[]
        {
            new ClientStage { User = user, Name = "Aguarda Agendamento de Visita", Color = "#94A3B8", Order = 0, IsFinal = false, IsWon = false, IsLost = false },
            new ClientStage { User = user, Name = "Visita Agendada",               Color = "#3B82F6", Order = 1, IsFinal = false, IsWon = false, IsLost = false },
            new ClientStage { User = user, Name = "Agendar Test Drive",             Color = "#8B5CF6", Order = 2, IsFinal = false, IsWon = false, IsLost = false },
            new ClientStage { User = user, Name = "Test Drive Marcado",             Color = "#F59E0B", Order = 3, IsFinal = false, IsWon = false, IsLost = false },
            new ClientStage { User = user, Name = "Aguarda Decisao",               Color = "#EF4444", Order = 4, IsFinal = false, IsWon = false, IsLost = false },
            new ClientStage { User = user, Name = "Venda",                         Color = "#10B981", Order = 5, IsFinal = true,  IsWon = true,  IsLost = false },
            new ClientStage { User = user, Name = "Perdido",                       Color = "#6B7280", Order = 6, IsFinal = true,  IsWon = false, IsLost = true  },
        };

        foreach (var stage in stages)
            _repo.AddStage(stage);

        stages[1].Templates = new List<StageNotificationTemplate>
        {
            new() { Stage = stages[1], UserId = user.Id, Title = "Confirmar visita", DaysAfter = 1 }
        };
        stages[4].Templates = new List<StageNotificationTemplate>
        {
            new() { Stage = stages[4], UserId = user.Id, Title = "Ligar ao cliente", DaysAfter = 2 }
        };
        stages[5].Templates = new List<StageNotificationTemplate>
        {
            new() { Stage = stages[5], UserId = user.Id, Title = "Contacto pos-venda", DaysAfter = 30 }
        };
    }

    private void SeedDefaultNotificationPreference(User user)
    {
        _repo.AddNotificationPreference(new NotificationPreference
        {
            User                            = user,
            DailyDigestTime                 = new TimeOnly(9, 29),
            DigestFrequencyDays             = 2,
            SaleFollowUpDays                = 30,
            DigestEnabled                   = true,
            StageChangeNotificationsEnabled = true,
            SaleNotificationsEnabled        = true
        });
    }

    private void SeedDefaultPublicProfile(User user)
    {
        _repo.AddPublicProfile(new UserPublicProfile
        {
            User                = user,
            ShowSalesCount      = false,
            ShowConversionRate  = false,
            ShowProposalsCount  = false,
            ShowHotDealsCount   = false,
            ShowAvgSaleValue    = false
        });
    }
}
