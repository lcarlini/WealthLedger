namespace WealthLedger.Infrastructure.Services;

/// <summary>
/// Money-weighted annual return (XIRR) over dated cash flows, solved by bisection.
/// Negative amounts are money put in, positive amounts are money taken out or the closing value.
/// </summary>
public static class Xirr
{
    private const double LowerBound = -0.99;
    private const double UpperBound = 10.0;

    /// <summary>Returns the annual rate (0.10 means 10%), or null when no rate can be found.</summary>
    public static double? Compute(IReadOnlyList<(DateTime Date, double Amount)> flows)
    {
        if (flows.Count < 2 || !flows.Any(f => f.Amount < 0) || !flows.Any(f => f.Amount > 0))
            return null;

        var start = flows.Min(f => f.Date);

        double Npv(double rate)
        {
            double sum = 0;
            foreach (var (date, amount) in flows)
            {
                var years = (date - start).TotalDays / 365.0;
                sum += amount / Math.Pow(1 + rate, years);
            }

            return sum;
        }

        var lo = LowerBound;
        var hi = UpperBound;
        var fLo = Npv(lo);
        var fHi = Npv(hi);
        if (double.IsNaN(fLo) || double.IsNaN(fHi) || Math.Sign(fLo) == Math.Sign(fHi))
            return null;

        for (var i = 0; i < 300; i++)
        {
            var mid = (lo + hi) / 2;
            var fMid = Npv(mid);
            if (double.IsNaN(fMid))
                return null;
            if (Math.Abs(fMid) < 1e-9)
                return mid;

            if (Math.Sign(fMid) == Math.Sign(fLo))
            {
                lo = mid;
                fLo = fMid;
            }
            else
            {
                hi = mid;
            }
        }

        return (lo + hi) / 2;
    }
}
