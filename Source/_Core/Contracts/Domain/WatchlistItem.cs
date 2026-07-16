using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Contracts.Domain;

/// <summary>Ticker watchlist with optional price alert threshold.</summary>
public class WatchlistItem : BaseEntity
{
    public string Ticker { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountType AccountType { get; set; } = AccountType.Stock;
    public decimal? TargetPrice { get; set; }
    public decimal? AlertAbove { get; set; }
    public decimal? AlertBelow { get; set; }
    public string? Notes { get; set; }
}
