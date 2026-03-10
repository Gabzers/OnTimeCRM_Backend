using OnTimeCRM.Domain.Common;

namespace OnTimeCRM.Domain.Entities;

public class Company : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? LogoUrl { get; set; }

    // Navigation
    public ICollection<Brand> Brands { get; set; } = new List<Brand>();
    public ICollection<User> Users { get; set; } = new List<User>();
}
