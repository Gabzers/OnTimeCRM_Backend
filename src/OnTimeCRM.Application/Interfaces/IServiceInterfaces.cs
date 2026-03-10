using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Sales;
using OnTimeCRM.Application.DTOs.Notifications;
using OnTimeCRM.Application.DTOs.Stages;
using OnTimeCRM.Application.DTOs.Vehicles;
using OnTimeCRM.Application.DTOs.Brands;
using OnTimeCRM.Application.DTOs.Companies;
using OnTimeCRM.Application.DTOs.Subscription;

namespace OnTimeCRM.Application.Interfaces;

public interface ISaleService
{
    Task<PagedResult<SaleListDto>> GetPagedAsync(Guid userId, SaleFilterParams filter, CancellationToken ct = default);
    Task<SaleDto> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
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
}

public interface IVehicleService
{
    Task<IEnumerable<VehicleBrandDto>> GetBrandsAsync(CancellationToken ct = default);
    Task<PagedResult<VehicleModelListDto>> GetModelsAsync(VehicleSearchParams p, CancellationToken ct = default);
    Task<VehicleModelDto> GetModelByIdAsync(Guid id, CancellationToken ct = default);
    Task<VehicleBrandDto> CreateBrandAsync(CreateVehicleBrandRequest request, CancellationToken ct = default);
    Task DeleteBrandAsync(Guid id, CancellationToken ct = default);
    Task<VehicleModelDto> CreateModelAsync(CreateVehicleModelRequest request, CancellationToken ct = default);
    Task<VehicleModelDto> UpdateModelAsync(Guid id, UpdateVehicleModelRequest request, CancellationToken ct = default);
    Task DeleteModelAsync(Guid id, CancellationToken ct = default);

    // Versions
    Task<IEnumerable<VehicleVersionDto>> GetVersionsAsync(Guid modelId, CancellationToken ct = default);
    Task<VehicleVersionDto> CreateVersionAsync(Guid modelId, CreateVehicleVersionRequest request, CancellationToken ct = default);
    Task<VehicleVersionDto> UpdateVersionAsync(Guid id, UpdateVehicleVersionRequest request, CancellationToken ct = default);
    Task DeleteVersionAsync(Guid id, CancellationToken ct = default);
}

public interface IBrandService
{
    Task<IEnumerable<BrandListDto>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<BrandDto> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);
    Task<BrandDto> CreateAsync(Guid companyId, CreateBrandRequest request, CancellationToken ct = default);
    Task<BrandDto> UpdateAsync(Guid id, Guid companyId, UpdateBrandRequest request, CancellationToken ct = default);
    Task SetActiveAsync(Guid id, Guid companyId, bool isActive, CancellationToken ct = default);
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
}
