namespace WealthLedger.Contracts.Api.Responses;

public class SimulationMatrixResponse
{
    public decimal StartingBalance { get; set; }
    public List<SimulationMonthColumn> MonthColumns { get; set; } = [];
    public List<SimulationRow> Rows { get; set; } = [];
    public List<decimal> TotalsPerMonth { get; set; } = [];
    public List<decimal> AccumulatedPerMonth { get; set; } = [];
}

public class SimulationMonthColumn
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class SimulationRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public List<decimal?> AmountsByMonth { get; set; } = [];
}
