namespace WealthLedger.Contracts.Api.Requests;

public class ArchiveInvestmentRequest
{
    /// <summary>Amount received on redemption. Defaults to current Amount when omitted.</summary>
    public decimal? RedeemedAmount { get; set; }

    /// <summary>Optional close date. Defaults to UTC now.</summary>
    public DateTime? ArchivedAt { get; set; }

    public string? Notes { get; set; }
}
