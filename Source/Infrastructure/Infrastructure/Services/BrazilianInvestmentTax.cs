namespace WealthLedger.Infrastructure.Services;

/// <summary>
/// Brazilian fixed-income tax helpers: regressive IR + IOF (first 30 days).
/// </summary>
public static class BrazilianInvestmentTax
{
    /// <summary>Regressive IR on fixed-income gains (CDB, Tesouro, etc.).</summary>
    public static decimal RegressiveIrRatePercent(int holdingDays)
    {
        if (holdingDays <= 180) return 22.5m;
        if (holdingDays <= 360) return 20m;
        if (holdingDays <= 720) return 17.5m;
        return 15m;
    }

    // Official regressive IOF table (Decreto 6.306/2007), days 1..29. It is NOT linear (96, 93, 90, 86, ...).
    private static readonly decimal[] IofTable =
    [
        96, 93, 90, 86, 83, 80, 76, 73, 70, 66,
        63, 60, 56, 53, 50, 46, 43, 40, 36, 33,
        30, 26, 23, 20, 16, 13, 10, 6, 3
    ];

    /// <summary>IOF on fixed-income gains for redemptions before 30 days (day 30+ = 0%).</summary>
    public static decimal IofRatePercent(int holdingDays)
    {
        if (holdingDays <= 0) return IofTable[0];
        if (holdingDays >= 30) return 0m;
        return IofTable[holdingDays - 1];
    }

    public static (decimal iofAmount, decimal iofRate, decimal irAmount, decimal irRate, decimal netGain)
        ApplyTaxes(decimal grossGain, int holdingDays, bool isIncomeTaxExempt, bool applyIof)
    {
        // A loss pays no tax but must stay visible: net = gross (negative), not 0.
        if (grossGain <= 0)
            return (0, 0, 0, 0, grossGain);

        var iofRate = applyIof ? IofRatePercent(holdingDays) : 0m;
        var iofAmount = Math.Round(grossGain * iofRate / 100m, 2, MidpointRounding.AwayFromZero);
        var taxableBase = grossGain - iofAmount;

        var irRate = isIncomeTaxExempt ? 0m : RegressiveIrRatePercent(holdingDays);
        var irAmount = Math.Round(taxableBase * irRate / 100m, 2, MidpointRounding.AwayFromZero);
        var netGain = grossGain - iofAmount - irAmount;
        return (iofAmount, iofRate, irAmount, irRate, netGain);
    }
}
