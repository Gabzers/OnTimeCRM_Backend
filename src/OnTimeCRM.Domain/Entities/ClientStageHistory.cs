using OnTimeCRM.Domain.Common;

namespace OnTimeCRM.Domain.Entities;

public class ClientStageHistory : BaseEntity
{
    public Guid ClientId { get; set; }
    public Guid UserId { get; set; }
    public Guid? FromStageId { get; set; }  // null on first entry
    public Guid ToStageId { get; set; }
    public string? Obs { get; set; }

    /// <summary>
    /// Abbreviated JSON snapshot of the active proposal at the time of the stage change.
    /// Format: { pid, pd, bt, pt, val, disc, tradeIn, ti, vehicles }
    /// </summary>
    public string? ProposalSnapshot { get; set; }

    // Navigation
    public Client Client { get; set; } = null!;
    public ClientStage? FromStage { get; set; }
    public ClientStage ToStage { get; set; } = null!;
}
