using OnTimeCRM.Domain.Common;

namespace OnTimeCRM.Domain.Entities;

public class VehicleBrand : BaseEntity
{
    public string Name { get; set; } = string.Empty;  // globally unique
    public string? LogoUrl { get; set; }

    // Navigation
    public ICollection<VehicleModel> Models { get; set; } = new List<VehicleModel>();
}
