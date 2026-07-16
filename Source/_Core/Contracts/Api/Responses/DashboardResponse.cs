namespace WealthLedger.Contracts.Api.Responses;

public class DashboardResponse
{
    public int InstitutionCount { get; set; }
    public int InvestmentCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PlannedDebitsProjection { get; set; }
    public int ProjectionYears { get; set; }
    public decimal DebitsNextMonth { get; set; }
    public decimal FutureCardDebits { get; set; }
    public List<InvestmentsByTypeItem> InvestmentsByType { get; set; } = [];
    public int PendingTaskCount { get; set; }
}

public class InvestmentsByTypeItem
{
    public string AccountType { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
}
