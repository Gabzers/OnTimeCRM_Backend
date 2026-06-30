using OnTime.Domain.Common;

namespace OnTime.Domain.Entities;

/// <summary>
/// A Stand (<see cref="Brand"/>) a user belongs to. <see cref="User.BrandId"/>/<see cref="User.CompanyId"/>
/// stay the "currently active" Stand carried in the JWT (cid/bid claims) — this table is the full
/// set the user is allowed to switch into via POST /api/users/me/switch-brand. See ARCHITECTURE.md.
/// </summary>
public class UserBrandMembership : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid BrandId { get; set; }

    public User User { get; set; } = null!;
    public Brand Brand { get; set; } = null!;
}
