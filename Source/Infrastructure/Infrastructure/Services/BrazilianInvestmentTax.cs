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

    /// <summary>
    /// IOF on fixed-income gains for redemptions before 30 days.
    /// Day 1 = 96%, then -3pp/day, day 30+ = 0%.
    /// </summary>
    public static decimal IofRatePercent(int holdingDays)
    {
        if (holdingDays <= 0) return 96m;
        if (holdingDays >= 30) return 0m;
        return 96m - (holdingDays - 1) * 3m;
    }

    public static (decimal iofAmount, decimal iofRate, decimal irAmount, decimal irRate, decimal netGain)
        ApplyTaxes(decimal grossGain, int holdingDays, bool isIncomeTaxExempt, bool applyIof)
    {
        if (grossGain <= 0)
            return (0, 0, 0, 0, 0);

        var iofRate = applyIof ? IofRatePercent(holdingDays) : 0m;
        var iofAmount = Math.Round(grossGain * iofRate / 100m, 2, MidpointRounding.AwayFromZero);
        var taxableBase = grossGain - iofAmount;

        var irRate = isIncomeTaxExempt ? 0m : RegressiveIrRatePercent(holdingDays);
        var irAmount = Math.Round(taxableBase * irRate / 100m, 2, MidpointRounding.AwayFromZero);
        var netGain = grossGain - iofAmount - irAmount;
        return (iofAmount, iofRate, irAmount, irRate, netGain);
    }
}
