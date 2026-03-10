using OnTimeCRM.Domain.Common;

namespace OnTimeCRM.Domain.Entities;

public class ProposalVehicle : BaseEntity
{
    public Guid ProposalId { get; set; }
    public Guid? ModelId { get; set; }         // null if free-text
    public string? FreeTextModel { get; set; }
    public bool IsPreferred { get; set; } = false;
    /// <summary>Price for this specific vehicle in this proposal.</summary>
    public decimal? Price { get; set; }
    /// <summary>Discount applied to this specific vehicle.</summary>
    public decimal? Discount { get; set; }
    public string? Obs { get; set; }
    public Guid? VersionId { get; set; }
    public string? ExternalColor { get; set; }
    public string? InternalColor { get; set; }

    // Navigation
    public Proposal Proposal { get; set; } = null!;
    public VehicleModel? Model { get; set; }
    public VehicleModelVersion? Version { get; set; }
}
