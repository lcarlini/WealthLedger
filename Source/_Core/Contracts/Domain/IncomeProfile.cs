namespace WealthLedger.Contracts.Domain;

/// <summary>
/// Stores the user's income configuration for simulation.
/// Income = BusinessDays × HoursPerDay × HourlyRateUsd × UsdBrlRate.
/// Net = Gross − (Gross × TaxPercent / 100).
/// </summary>
public class IncomeProfile : BaseEntity
{
    public decimal HourlyRateUsd { get; set; }
    public int HoursPerDay { get; set; } = 8;
    public decimal UsdBrlRate { get; set; }
    public decimal TaxPercent { get; set; }
}
