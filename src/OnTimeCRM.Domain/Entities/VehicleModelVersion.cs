using OnTimeCRM.Domain.Common;

namespace OnTimeCRM.Domain.Entities;

/// <summary>
/// A specific version/trim of a <see cref="VehicleModel"/>
/// (e.g. "4x4 580cv" vs "4x2 300cv" for the same base model).
/// External and internal color lists are stored as JSON arrays.
/// </summary>
public class VehicleModelVersion : BaseEntity
{
    public Guid ModelId { get; set; }
    public string Name { get; set; } = string.Empty;

    // JSON arrays, e.g. ["Branco","Preto","Cinzento"]
    public string ExternalColors { get; set; } = "[]";
    public string InternalColors { get; set; } = "[]";

    // Navigation
    public VehicleModel Model { get; set; } = null!;
}
