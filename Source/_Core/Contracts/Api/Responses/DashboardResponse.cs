namespace WealthLedger.Contracts.Api.Responses;

public class DashboardResponse
{
    public int InstitutionCount { get; set; }
    public int InvestmentCount { get; set; }
    public int ArchivedInvestmentCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalPrincipal { get; set; }
    public decimal UnrealizedGain { get; set; }
    public decimal PlannedDebitsProjection { get; set; }
    public int ProjectionYears { get; set; }
    public decimal DebitsNextMonth { get; set; }
    public decimal FutureCardDebits { get; set; }
    public List<InvestmentsByTypeItem> InvestmentsByType { get; set; } = [];
    public int PendingTaskCount { get; set; }

    /// <summary>Weighted estimated annual return of active fixed-income holdings.</summary>
    public decimal? EstimatedAnnualReturnPercent { get; set; }
    public decimal? SelicPercent { get; set; }
    public decimal? IpcaPercent { get; set; }
    public decimal? ExcessOverCdiPercent { get; set; }
    public decimal? ExcessOverIpcaPercent { get; set; }
    public decimal ProjectedValue1Y { get; set; }
    public decimal ProjectedValue3Y { get; set; }
    public decimal ProjectedValue5Y { get; set; }
    public string PortfolioVerdict { get; set; } = string.Empty;
    public List<DashboardHoldingHighlight> TopHoldings { get; set; } = [];
}

public class DashboardHoldingHighlight
{
    public Guid InvestmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal AmountBrl { get; set; }
    public decimal GainPercent { get; set; }
    public string RateLabel { get; set; } = string.Empty;
}

public class InvestmentsByTypeItem
{
    public string AccountType { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
}
