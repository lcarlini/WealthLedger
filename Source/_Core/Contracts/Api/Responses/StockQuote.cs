namespace WealthLedger.Contracts.Api.Responses;

public class StockQuote
{
    public string Ticker { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "BRL";
    public DateTime? AsOf { get; set; }
    public string? Source { get; set; }
}
