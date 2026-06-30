using OnTime.Domain.Common;

namespace OnTime.Domain.Entities;

public class Brand : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }  // hex e.g. "#1C69D4"
    /// <summary>False hides vehicle-related UI (Veículos, vehicle pickers) for this Stand's
    /// users — UI-hide only, no change to the underlying vehicle/proposal model. See ROADMAP.md.</summary>
    public bool IsAutomotive { get; set; } = true;

    // Navigation
    public Company Company { get; set; } = null!;
    public ICollection<User> Users { get; set; } = new List<User>();
}
