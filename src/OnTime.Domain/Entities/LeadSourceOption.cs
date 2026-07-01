using OnTime.Domain.Common;

namespace OnTime.Domain.Entities;

/// <summary>
/// Per-user lead source option (e.g. "Stand", "Instagram"). Each user maintains their own list.
/// <see cref="Code"/> is the value stored on <see cref="Client.LeadSource"/>, unique per user.
/// </summary>
public class LeadSourceOption : BaseEntity
{
    public Guid UserId { get; set; }
    public int Code { get; set; }
    public string Name { get; set; } = string.Empty;

    public User User { get; set; } = null!;
}
