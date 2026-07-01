using OnTime.Domain.Common;

namespace OnTime.Domain.Entities;

public class ClientStageTemperatureRule : BaseEntity
{
    public Guid StageId { get; set; }
    public int DaysAfterEntry { get; set; }  // 0 = applies immediately on stage entry
    public int Temperature { get; set; }     // DealTemperature: Hot=0 Warm=1 Cold=2

    // Navigation
    public ClientStage Stage { get; set; } = null!;
}
