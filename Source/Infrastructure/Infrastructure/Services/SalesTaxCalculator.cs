using WealthLedger.Contracts.Api.Trades;

namespace WealthLedger.Infrastructure.Services;

public enum TaxCategory
{
    Stock,
    Fii,
    Etf,
    Bdr,
    Crypto,
    Other,
}

/// <summary>A realised sale, with the category that decides its tax treatment.</summary>
public sealed record TaxableSale(TaxCategory Category, DateTime Date, decimal Proceeds, decimal Gain);

/// <summary>
/// Monthly capital-gains estimate for Brazilian sales. Rules are encoded as data in <see cref="RuleFor"/>.
/// These are the rules as understood when this was written: the monthly sales exemptions and the
/// rates may change, and this is an estimate, not tax advice. Check the current rules with an accountant.
///
/// Modelled assumptions:
/// - Each category is taxed on its own; losses offset gains of the same category only.
/// - Losses carry forward indefinitely within the category.
/// - Stocks (15%) and crypto (15%) are exempt in a month when total sales volume is at or below the limit
///   (R$ 20,000 for stocks, R$ 35,000 for crypto). Losses in an exempt month still carry forward.
/// - FII is taxed at 20% with no exemption. ETF and BDR are taxed at 15% with no exemption.
/// - Day trade, IRRF withholding and funds are not modelled; funds are reported as not computed.
/// - DARF due date is the last weekday of the following month (national holidays are not considered).
/// </summary>
public static class SalesTaxCalculator
{
    public const string Disclaimer =
        "Estimate only, based on the rules modelled in the application. It does not replace an accountant " +
        "or the official DARF calculation. Day trade, IRRF withholding and funds are not included.";

    private sealed record Rule(decimal RatePercent, decimal? ExemptVolumeAtOrBelow);

    private static Rule? RuleFor(TaxCategory category) => category switch
    {
        TaxCategory.Stock => new Rule(15m, 20_000m),
        TaxCategory.Fii => new Rule(20m, null),
        TaxCategory.Etf => new Rule(15m, null),
        TaxCategory.Bdr => new Rule(15m, null),
        TaxCategory.Crypto => new Rule(15m, 35_000m),
        _ => null,
    };

    public static IReadOnlyList<TaxMonth> Compute(IEnumerable<TaxableSale> sales)
    {
        var results = new List<TaxMonth>();

        foreach (var byCategory in sales.GroupBy(s => s.Category))
        {
            var rule = RuleFor(byCategory.Key);
            var label = byCategory.Key.ToString();

            if (rule is null)
            {
                foreach (var month in byCategory.GroupBy(MonthOf).OrderBy(g => g.Key))
                {
                    results.Add(new TaxMonth(label, month.Key, month.Sum(s => s.Proceeds), month.Sum(s => s.Gain),
                        0m, 0m, false, 0m, null, 0m, null));
                }

                continue;
            }

            decimal carry = 0m;
            foreach (var month in byCategory.GroupBy(MonthOf).OrderBy(g => g.Key))
            {
                var volume = month.Sum(s => s.Proceeds);
                var net = month.Sum(s => s.Gain);
                var exempt = rule.ExemptVolumeAtOrBelow is { } limit && volume <= limit;
                var carryIn = carry;
                decimal taxable = 0m;

                if (net < 0m)
                {
                    carry += -net;
                }
                else if (!exempt)
                {
                    taxable = Math.Max(0m, net - carry);
                    carry = Math.Max(0m, carry - net);
                }

                var tax = Math.Round(taxable * rule.RatePercent / 100m, 2, MidpointRounding.AwayFromZero);

                results.Add(new TaxMonth(
                    Category: label,
                    Month: month.Key,
                    SalesVolume: volume,
                    NetGain: net,
                    LossCarriedIn: carryIn,
                    LossCarriedOut: carry,
                    ExemptByThreshold: exempt,
                    TaxableBase: taxable,
                    RatePercent: rule.RatePercent,
                    TaxDue: tax,
                    DarfDueDate: tax > 0m ? DarfDue(month.Key) : null));
            }
        }

        return results
            .OrderBy(r => r.Month, StringComparer.Ordinal)
            .ThenBy(r => r.Category, StringComparer.Ordinal)
            .ToList();
    }

    private static string MonthOf(TaxableSale sale) => sale.Date.ToString("yyyy-MM");

    /// <summary>Last weekday of the month after <paramref name="month"/> (yyyy-MM).</summary>
    public static DateOnly DarfDue(string month)
    {
        var first = DateOnly.ParseExact(month + "-01", "yyyy-MM-dd");
        var lastOfNext = first.AddMonths(2).AddDays(-1);
        while (lastOfNext.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            lastOfNext = lastOfNext.AddDays(-1);
        return lastOfNext;
    }
}
