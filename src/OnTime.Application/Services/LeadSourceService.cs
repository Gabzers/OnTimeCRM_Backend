using OnTime.Application.Common;
using OnTime.Application.DTOs.LeadSources;
using OnTime.Application.Interfaces;
using OnTime.Application.Interfaces.Repositories;
using OnTime.Domain.Entities;

namespace OnTime.Application.Services;

public class LeadSourceService : ILeadSourceService
{
    private readonly ILeadSourceRepository _repo;
    private readonly IUnitOfWork _uow;

    public LeadSourceService(ILeadSourceRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow  = uow;
    }

    public async Task<IEnumerable<LeadSourceOptionDto>> GetByUserAsync(
        Guid userId, CancellationToken ct = default)
    {
        var options = await _repo.GetByUserAsync(userId, ct);
        return options.Select(ToDto);
    }

    public async Task<LeadSourceOptionDto> CreateAsync(
        Guid userId, CreateLeadSourceRequest req, CancellationToken ct = default)
    {
        var code = await _repo.GetNextCodeAsync(userId, ct);
        var option = new LeadSourceOption { UserId = userId, Code = code, Name = req.Name };
        _repo.Add(option);
        await _uow.SaveChangesAsync(ct);
        return ToDto(option);
    }

    public async Task<LeadSourceOptionDto> UpdateAsync(
        Guid id, Guid userId, UpdateLeadSourceRequest req, CancellationToken ct = default)
    {
        var option = await RequireOwnedAsync(id, userId, ct);
        option.Name = req.Name;
        await _uow.SaveChangesAsync(ct);
        return ToDto(option);
    }

    public async Task SetActiveAsync(
        Guid id, Guid userId, bool isActive, CancellationToken ct = default)
    {
        var option = await RequireOwnedAsync(id, userId, ct);
        option.IsActive = isActive;
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<LeadSourceOption> RequireOwnedAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var option = await _repo.FindAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.LEAD_SOURCE_NOT_FOUND);
        if (option.UserId != userId)
            throw new ApiException(ApiErrorCatalog.LEAD_SOURCE_WRONG_USER);
        return option;
    }

    private static LeadSourceOptionDto ToDto(LeadSourceOption o) =>
        new(o.Id, o.Code, o.Name, o.IsActive);
}
