namespace OnTime.Domain.Enums;

public enum LeadSource
{
    WalkIn = 0,
    Phone = 1,
    OLX = 2,
    Standvirtual = 3,
    Instagram = 4,
    Facebook = 5,
    Referral = 6,
    Other = 7
}

public enum BusinessType
{
    DirectPurchase = 0,
    TradeIn = 1,
    TradeInWithDifference = 2,
    Leasing = 3,
    Financing = 4
}

public enum PaymentType
{
    Cash = 0,
    Financing = 1,
    Leasing = 2,
    BankTransfer = 3,
    Other = 4
}

public enum ProposalStatus
{
    Active = 0,
    Won = 1,       // converted to sale
    Lost = 2,
    Cancelled = 3
}

public enum LossReason
{
    Price = 0,
    Competition = 1,
    NoDecision = 2,
    FinancingRejected = 3,
    VehicleNotAvailable = 4,
    ClientUnreachable = 5,
    Other = 6
}

public enum GoalMetricType
{
    NewClients = 0,
    Sales = 1,
    Proposals = 2,
    ConversionRate = 3
}

public enum GoalPeriod
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
    Annual = 3
}

public enum TradeInType
{
    Car = 0,
    Motorcycle = 1,
    Van = 2,
    Truck = 3,
    Other = 4
}

public enum DealTemperature
{
    Hot = 0,   // interaction within last 3 days
    Warm = 1,  // 4–10 days
    Cold = 2   // 10+ days or no interaction
}

public enum SummaryFrequency
{
    Weekly = 0,
    Monthly = 1
}
