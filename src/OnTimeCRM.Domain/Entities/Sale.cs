using OnTimeCRM.Domain.Common;
using OnTimeCRM.Domain.Enums;

namespace OnTimeCRM.Domain.Entities;

public class Sale : BaseEntity
{
    public Guid ProposalId { get; set; }
    public Guid ClientId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ModelId { get; set; }
    public string? FreeTextModel { get; set; }
    public decimal FinalValue { get; set; }
    public PaymentType PaymentType { get; set; }

    /// <summary>
    /// User-provided business date — when the deal actually closed.
    /// NEVER auto-set to UtcNow. Must always come from the request.
    /// </summary>
    public DateTimeOffset SoldAt { get; set; }

    public string? Plate { get; set; }
    public string? Chassis { get; set; }
    public string? Obs { get; set; }
    /// <summary>
    /// Salesperson commission for this sale. Private — never shared with friends.
    /// Nullable: salesperson can choose not to fill this.
    /// </summary>
    public decimal? Commission { get; set; }

    // Navigation
    public Proposal Proposal { get; set; } = null!;
    public Client Client { get; set; } = null!;
    public User User { get; set; } = null!;
    public VehicleModel? Model { get; set; }
}
