namespace OnTimeCRM.Domain.Enums;

public enum UserRole
{
    Salesperson = 0,
    Manager = 1,  // sees all salespeople's data within their Brand
    Admin = 2     // platform superadmin — bypasses subscription and permission checks
}

public enum UserAccountStatus
{
    PendingActivation = 0,  // registered but not yet activated
    Active = 1,             // subscription valid, full access
    Expired = 2,            // subscription end date passed, read-only access
    Inactive = 3,           // manually deactivated
    Suspended = 4,          // suspended by platform (e.g. payment failure after grace period)
    Cancelled = 5           // subscription explicitly cancelled, account archived
}
