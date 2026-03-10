using OnTimeCRM.Domain.Common;

namespace OnTimeCRM.Domain.Entities;

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

    // Navigation
    public Company Company { get; set; } = null!;
    public ICollection<User> Users { get; set; } = new List<User>();
}
