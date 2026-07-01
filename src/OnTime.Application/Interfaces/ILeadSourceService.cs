using OnTime.Application.DTOs.LeadSources;

namespace OnTime.Application.Interfaces;

public interface ILeadSourceService
{
    Task<IEnumerable<LeadSourceOptionDto>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<LeadSourceOptionDto> CreateAsync(Guid userId, CreateLeadSourceRequest req, CancellationToken ct = default);
    Task<LeadSourceOptionDto> UpdateAsync(Guid id, Guid userId, UpdateLeadSourceRequest req, CancellationToken ct = default);
    Task SetActiveAsync(Guid id, Guid userId, bool isActive, CancellationToken ct = default);
}
