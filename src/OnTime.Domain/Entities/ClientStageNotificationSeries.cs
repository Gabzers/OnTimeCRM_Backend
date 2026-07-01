using OnTime.Domain.Common;

namespace OnTime.Domain.Entities;

/// <summary>
/// Tracks a recurring StageNotificationTemplate's progress for one client's current stay in a
/// stage. Keyed off ClientStageHistory.Id so re-entering the same stage later (e.g. via "Nova
/// Oportunidade") starts a fresh series from occurrence 1 — a new stage entry is always a new
/// ClientStageHistory row.
/// </summary>
public class ClientStageNotificationSeries : BaseEntity
{
    public Guid ClientStageHistoryId { get; set; }
    public Guid TemplateId { get; set; }
    public int OccurrenceCount { get; set; } = 0;
    public bool IsActive { get; set; } = true;  // false once stopped (stage change, Won/Lost, or MaxOccurrences reached)
    public DateTimeOffset? LastFiredAt { get; set; }

    // Navigation
    public ClientStageHistory ClientStageHistory { get; set; } = null!;
    public StageNotificationTemplate Template { get; set; } = null!;
}
