using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Contracts.Api.Responses;

/// <summary>
/// Verdict card for a single holding: applied → now, taxes, inflation, vs CDI.
/// </summary>
public class InvestmentPerformanceResponse
{
    public Guid InvestmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public Currency Currency { get; set; }
    public InvestmentStatus Status { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime AsOfDate { get; set; }
    public int HoldingDays { get; set; }

    public decimal PrincipalAmount { get; set; }
    public decimal CurrentAmount { get; set; }

    /// <summary>Theoretical value accruing Principal at CDI% × historical CDI (fixed income BRL).</summary>
    public decimal? AccruedTheoreticalAmount { get; set; }

    public decimal GrossGain { get; set; }
    public decimal GrossGainPercent { get; set; }

    public decimal IofAmount { get; set; }
    public decimal IofRatePercent { get; set; }
    public decimal IrAmount { get; set; }
    public decimal IrRatePercent { get; set; }
    public bool IsIncomeTaxExempt { get; set; }

    public decimal NetAmount { get; set; }
    public decimal NetGain { get; set; }
    public decimal NetGainPercent { get; set; }

    public decimal? CdiAccumulatedPercent { get; set; }
    public decimal? CdiEquivalentAccumulatedPercent { get; set; }
    public decimal? IpcaAccumulatedPercent { get; set; }
    public decimal? RealGainPercent { get; set; }

    /// <summary>Net gain % minus IPCA accumulated over the same window.</summary>
    public decimal? ExcessOverIpcaPercent { get; set; }

    /// <summary>Net gain % minus 100% CDI accumulated over the same window.</summary>
    public decimal? ExcessOverCdiPercent { get; set; }

    /// <summary>beat_cdi | match | lag_cdi | beat_ipca | lag_ipca style labels for UI.</summary>
    public string Verdict { get; set; } = string.Empty;
    public string VerdictSummary { get; set; } = string.Empty;

    public List<PerformanceBreakdownItem> Breakdown { get; set; } = [];
    public List<PerformanceProjectionPoint> Projections { get; set; } = [];
}

public class PerformanceBreakdownItem
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}

public class PerformanceProjectionPoint
{
    public int MonthsAhead { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal ProjectedGross { get; set; }
    public decimal ProjectedNet { get; set; }
    public decimal ProjectedNetGainPercent { get; set; }
}
