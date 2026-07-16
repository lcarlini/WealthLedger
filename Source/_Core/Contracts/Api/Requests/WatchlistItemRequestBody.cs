using WealthLedger.Contracts.Domain.Enums;
using WealthLedger.Contracts.Domain.Interfaces;

namespace WealthLedger.Contracts.Api.Requests;

public class WatchlistItemRequestBody : IEntityRequestBody
{
    public Guid Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountType AccountType { get; set; } = AccountType.Stock;
    public decimal? TargetPrice { get; set; }
    public decimal? AlertAbove { get; set; }
    public decimal? AlertBelow { get; set; }
    public string? Notes { get; set; }
}
