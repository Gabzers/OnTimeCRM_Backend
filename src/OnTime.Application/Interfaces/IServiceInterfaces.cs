using OnTime.Application.Common;
using OnTime.Application.DTOs.Sales;
using OnTime.Application.DTOs.Notifications;
using OnTime.Application.DTOs.Stages;
using OnTime.Application.DTOs.Vehicles;
using OnTime.Application.DTOs.Brands;
using OnTime.Application.DTOs.Companies;
using OnTime.Application.DTOs.Subscription;
using OnTime.Application.DTOs.Users;

namespace OnTime.Application.Interfaces;

public interface ISaleService
{
    Task<PagedResult<SaleListDto>> GetPagedAsync(Guid userId, SaleFilterParams filter, CancellationToken ct = default);
    Task<SaleDto> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<SaleDto> UpdateAsync(Guid id, Guid userId, UpdateSaleRequest request, CancellationToken ct = default);
    Task<DashboardDto> GetDashboardAsync(Guid userId, CancellationToken ct = default);
}

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetPagedAsync(Guid userId, NotificationFilterParams filter, CancellationToken ct = default);
    Task<IEnumerable<NotificationDto>> GetTodayAsync(Guid userId, CancellationToken ct = default);
    Task<int> GetOverdueCountAsync(Guid userId, CancellationToken ct = default);
    Task<NotificationDto> CreateAsync(Guid userId, CreateNotificationRequest request, CancellationToken ct = default);
    Task MarkDoneAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task SnoozeAsync(Guid id, Guid userId, SnoozeNotificationRequest request, CancellationToken ct = default);
    Task IgnoreAsync(Guid id, Guid userId, CancellationToken ct = default);
}

public interface IClientStageService
{
    Task<IEnumerable<ClientStageDto>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<ClientStageDto> CreateAsync(Guid userId, CreateStageRequest request, CancellationToken ct = default);
    Task<ClientStageDto> UpdateAsync(Guid id, Guid userId, UpdateStageRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task ReorderAsync(Guid userId, ReorderStagesRequest request, CancellationToken ct = default);
    Task<StageTemplateDto> AddTemplateAsync(Guid stageId, Guid userId, CreateStageTemplateRequest request, CancellationToken ct = default);
    Task<StageTemplateDto> UpdateTemplateAsync(Guid stageId, Guid templateId, Guid userId, UpdateStageTemplateRequest request, CancellationToken ct = default);
    Task DeleteTemplateAsync(Guid stageId, Guid templateId, Guid userId, CancellationToken ct = default);
    Task<TemperatureRuleDto> AddTemperatureRuleAsync(Guid stageId, Guid userId, CreateTemperatureRuleRequest request, CancellationToken ct = default);
    Task<TemperatureRuleDto> UpdateTemperatureRuleAsync(Guid stageId, Guid ruleId, Guid userId, UpdateTemperatureRuleRequest request, CancellationToken ct = default);
    Task DeleteTemperatureRuleAsync(Guid stageId, Guid ruleId, Guid userId, CancellationToken ct = default);
}

public interface IVehicleService
{
    Task<IEnumerable<VehicleBrandDto>> GetBrandsAsync(CancellationToken ct = default);
    Task<PagedResult<VehicleModelListDto>> GetModelsAsync(VehicleSearchParams p, Guid userId, CancellationToken ct = default);
    Task<VehicleModelDto> GetModelByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<VehicleBrandDto> CreateBrandAsync(CreateVehicleBrandRequest request, CancellationToken ct = default);
    Task DeleteBrandAsync(Guid id, CancellationToken ct = default);
    Task<VehicleModelDto> CreateModelAsync(CreateVehicleModelRequest request, Guid userId, CancellationToken ct = default);
    Task<VehicleModelDto> UpdateModelAsync(Guid id, Guid userId, UpdateVehicleModelRequest request, CancellationToken ct = default);
    Task SetModelActiveAsync(Guid id, Guid userId, bool isActive, CancellationToken ct = default);
    Task DeleteModelAsync(Guid id, Guid userId, CancellationToken ct = default);

    // Versions
    Task<IEnumerable<VehicleVersionDto>> GetVersionsAsync(Guid modelId, Guid userId, CancellationToken ct = default);
    Task<VehicleVersionDto> CreateVersionAsync(Guid modelId, Guid userId, CreateVehicleVersionRequest request, CancellationToken ct = default);
    Task<VehicleVersionDto> UpdateVersionAsync(Guid id, Guid userId, UpdateVehicleVersionRequest request, CancellationToken ct = default);
    Task DeleteVersionAsync(Guid id, Guid userId, CancellationToken ct = default);
}

public interface IBrandService
{
    Task<IEnumerable<BrandListDto>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<BrandDto> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);
    Task<BrandDto> CreateAsync(Guid companyId, CreateBrandRequest request, CancellationToken ct = default);
    Task<BrandDto> UpdateAsync(Guid id, Guid companyId, UpdateBrandRequest request, CancellationToken ct = default);
    Task SetActiveAsync(Guid id, Guid companyId, bool isActive, CancellationToken ct = default);

    Task<BrandVehicleBrandsDto> GetVehicleBrandIdsAsync(Guid brandId, Guid companyId, CancellationToken ct = default);
    Task SetVehicleBrandIdsAsync(Guid brandId, Guid companyId, UpdateBrandVehicleBrandsRequest request, CancellationToken ct = default);

    Task GrantMembershipAsync(Guid brandId, Guid companyId, Guid userId, CancellationToken ct = default);
    Task RevokeMembershipAsync(Guid brandId, Guid companyId, Guid userId, CancellationToken ct = default);
}

public interface ISubscriptionService
{
    Task<SubscriptionStatusDto> GetStatusAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<SubscriptionPaymentDto>> GetPaymentsAsync(Guid userId, CancellationToken ct = default);
    Task<InitiateSubscriptionResponseDto> InitiateAsync(Guid userId, InitiateSubscriptionRequest request, CancellationToken ct = default);
    Task<SubscriptionPaymentDto> GetPaymentStatusAsync(Guid paymentId, Guid userId, CancellationToken ct = default);
    Task CancelAsync(Guid userId, CancellationToken ct = default);
}

public interface INotificationPreferenceService
{
    Task<NotificationPreferenceDto> GetAsync(Guid userId, CancellationToken ct = default);
    Task<NotificationPreferenceDto> UpdateAsync(Guid userId, UpdateNotificationPreferenceRequest request, CancellationToken ct = default);
}

public interface IAdminService
{
    Task<IEnumerable<CompanyAdminDto>> GetCompaniesAsync(CancellationToken ct = default);
    Task<CompanyAdminDto> CreateCompanyAsync(CreateCompanyAdminRequest request, CancellationToken ct = default);
    Task<CompanyAdminDto> UpdateCompanyAsync(Guid id, UpdateCompanyAdminRequest request, CancellationToken ct = default);
    Task SetCompanyActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
    Task<IEnumerable<UserListDto>> GetUsersByCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<UserListDto> UpdateUserRoleAsync(Guid userId, int role, Guid actingUserId, CancellationToken ct = default);

    Task GrantMembershipAsync(Guid userId, Guid brandId, CancellationToken ct = default);
    Task RevokeMembershipAsync(Guid userId, Guid brandId, CancellationToken ct = default);
}
