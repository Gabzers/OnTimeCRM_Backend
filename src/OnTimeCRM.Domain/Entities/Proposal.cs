using OnTimeCRM.Domain.Common;
using OnTimeCRM.Domain.Enums;

namespace OnTimeCRM.Domain.Entities;

public class Proposal : BaseEntity
{
    public Guid ClientId { get; set; }
    public Guid UserId { get; set; }
    public ProposalStatus Status { get; set; } = ProposalStatus.Active;
    public BusinessType BusinessType { get; set; }
    public PaymentType PaymentType { get; set; }
    public decimal? ProposalValue { get; set; }
    public decimal? Discount { get; set; }
    public LossReason? LossReason { get; set; }
    public string? LossNotes { get; set; }
    public DateTimeOffset? WonAt { get; set; }
    public DateTimeOffset? LostAt { get; set; }

    /// <summary>
    /// User-controlled business date — when the proposal was actually presented to the client.
    /// Defaults to UtcNow in the service if not provided. NOT a system timestamp.
    /// </summary>
    public DateTimeOffset? ProposalDate { get; set; }

    // Trade-in
    public bool HasTradeIn { get; set; } = false;
    public TradeInType? TradeInType { get; set; }
    public string? TradeInPlate { get; set; }
    public string? TradeInBrand { get; set; }
    public string? TradeInModel { get; set; }
    public int? TradeInYear { get; set; }
    public int? TradeInKm { get; set; }
    public decimal? TradeInEstimatedValue { get; set; }

    // Navigation
    public Client Client { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<ProposalVehicle> Vehicles { get; set; } = new List<ProposalVehicle>();
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
