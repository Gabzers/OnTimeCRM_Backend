using OnTimeCRM.Domain.Common;

namespace OnTimeCRM.Domain.Entities;

public class ClientStage : BaseEntity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }  // hex
    public int Order { get; set; }      // 0-based
    public bool IsFinal { get; set; } = false;
    public bool IsWon { get; set; } = false;
    public bool IsLost { get; set; } = false;

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<Client> Clients { get; set; } = new List<Client>();
    public ICollection<StageNotificationTemplate> Templates { get; set; } = new List<StageNotificationTemplate>();
}
