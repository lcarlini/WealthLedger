using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Contracts.Domain;

/// <summary>A single buy or sell of a variable-income holding. Positions are derived from these rows.</summary>
public class TradeTransaction : BaseEntity
{
    public Guid InvestmentId { get; set; }
    public TradeSide Side { get; set; }
    public DateTime TradeDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Fees { get; set; }
    public string? Notes { get; set; }
}
