namespace WealthLedger.Contracts.Api.Responses;

public class RefreshPricesResult
{
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<RefreshPriceItemResult> Items { get; set; } = [];
}

public class RefreshPriceItemResult
{
    public Guid InvestmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Ticker { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public decimal? PreviousAmount { get; set; }
    public decimal? NewAmount { get; set; }
    public decimal? Price { get; set; }
    public string? QuoteCurrency { get; set; }
}
