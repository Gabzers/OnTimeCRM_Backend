using OnTimeCRM.Domain.Common;
using OnTimeCRM.Domain.Enums;

namespace OnTimeCRM.Domain.Entities;

public class VehicleModel : BaseEntity
{
    public Guid BrandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public int? Year { get; set; }
    public FuelType? FuelType { get; set; }
    public decimal? BasePrice { get; set; }
    public string? ImageUrl { get; set; }

    // Navigation
    public VehicleBrand Brand { get; set; } = null!;
    public ICollection<ProposalVehicle> ProposalVehicles { get; set; } = new List<ProposalVehicle>();
    public ICollection<VehicleModelVersion> Versions { get; set; } = new List<VehicleModelVersion>();
}
