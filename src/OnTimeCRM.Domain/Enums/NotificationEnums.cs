namespace OnTimeCRM.Domain.Enums;

public enum NotificationStatus
{
    Pending = 0,
    Done = 1,
    Snoozed = 2,
    Ignored = 3
}

public enum NotificationTrigger
{
    Manual = 0,
    StageChanged = 1,
    SaleClosed = 2,
    ProposalCreated = 3,
    Custom = 4
}
