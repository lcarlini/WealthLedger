namespace WealthLedger.Contracts.Domain.Enums;

/// <summary>
/// Lifecycle of a holding. Archived keeps history after redemption/close.
/// </summary>
public enum InvestmentStatus
{
    Active = 1,
    Archived = 2,
}
