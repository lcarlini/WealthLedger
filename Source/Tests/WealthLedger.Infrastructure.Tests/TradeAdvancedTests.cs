using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;
using WealthLedger.Infrastructure.Services;
using WealthLedger.Contracts.Api.Trades;
using Xunit;

namespace WealthLedger.Infrastructure.Tests;

public sealed class TradeCostMethodTests
{
    private static readonly Guid Inv = Guid.NewGuid();

    private static TradeTransaction T(TradeSide side, decimal qty, decimal price, string date, decimal fees = 0m) => new()
    {
        Id = Guid.NewGuid(),
        InvestmentId = Inv,
        Side = side,
        TradeDate = DateTime.Parse(date),
        Quantity = qty,
        UnitPrice = price,
        Fees = fees,
        CreatedDate = DateTime.Parse("2026-01-01"),
        UpdatedDate = DateTime.Parse("2026-01-01"),
    };

    [Fact]
    public void Weighted_average_and_fifo_give_different_realised_gains_on_the_same_sale()
    {
        var trades = new[]
        {
            T(TradeSide.Buy, 10, 10, "2026-01-02"),
            T(TradeSide.Buy, 10, 20, "2026-01-03"),
            T(TradeSide.Sell, 10, 30, "2026-01-04"),
        };

        var weighted = TradePositionCalculator.Calculate(Inv, trades, CostMethod.WeightedAverage);
        var fifo = TradePositionCalculator.Calculate(Inv, trades, CostMethod.Fifo);

        Assert.Equal(150m, weighted.RealizedPnl); // avg cost 15 -> (30-15)*10
        Assert.Equal(200m, fifo.RealizedPnl);     // oldest lot cost 10 -> (30-10)*10
        Assert.Equal(weighted.Quantity, fifo.Quantity);
        // Remaining cost differs by method: 10 shares at 15 (average) versus 10 shares at 20 (FIFO).
        Assert.Equal(150m, weighted.CostBasis);
        Assert.Equal(200m, fifo.CostBasis);
    }

    [Fact]
    public void Bonus_shares_add_quantity_at_zero_cost_and_lower_the_average()
    {
        var position = TradePositionCalculator.Calculate(Inv, [
            T(TradeSide.Buy, 10, 10, "2026-01-02"),
            T(TradeSide.Bonus, 10, 0, "2026-01-05"),
        ]);

        Assert.Equal(20m, position.Quantity);
        Assert.Equal(100m, position.CostBasis);
        Assert.Equal(5m, position.AveragePrice);
        Assert.Equal(0m, position.RealizedPnl);
    }

    [Fact]
    public void Dividends_are_income_per_share_held_on_the_date()
    {
        var detail = TradePositionCalculator.CalculateDetailed(Inv, [
            T(TradeSide.Buy, 10, 10, "2026-01-02"),
            T(TradeSide.Dividend, 0, 2m, "2026-02-01"),
            T(TradeSide.Sell, 4, 12, "2026-02-10"),
            T(TradeSide.Dividend, 0, 1m, "2026-03-01"),
        ]);

        Assert.Equal(20m + 6m, detail.Summary.DividendIncome); // 10 shares * 2, then 6 shares * 1
        Assert.Equal(2, detail.Dividends.Count);
    }

    [Fact]
    public void Dividend_with_no_shares_held_is_zero()
    {
        var position = TradePositionCalculator.Calculate(Inv, [T(TradeSide.Dividend, 0, 2m, "2026-02-01")]);

        Assert.Equal(0m, position.DividendIncome);
    }

    [Fact]
    public void Fifo_sell_spanning_two_lots_uses_the_older_lot_first()
    {
        var detail = TradePositionCalculator.CalculateDetailed(Inv, [
            T(TradeSide.Buy, 5, 10, "2026-01-02"),
            T(TradeSide.Buy, 5, 30, "2026-01-03"),
            T(TradeSide.Sell, 7, 40, "2026-01-04"),
        ], CostMethod.Fifo);

        // 5 from lot one (cost 50) and 2 from lot two (cost 60): cost removed 110, proceeds 280.
        Assert.Equal(110m, detail.Sales.Single().CostRemoved);
        Assert.Equal(170m, detail.Summary.RealizedPnl);
        Assert.Equal(3m, detail.Summary.Quantity);
        Assert.Equal(90m, detail.Summary.CostBasis); // remaining 3 shares of lot two, 30 each
    }
}

public sealed class XirrTests
{
    [Fact]
    public void One_year_doubling_of_ten_percent()
    {
        var rate = Xirr.Compute([
            (new DateTime(2025, 1, 1), -100d),
            (new DateTime(2026, 1, 1), 110d),
        ]);

        Assert.NotNull(rate);
        Assert.Equal(0.10, rate!.Value, precision: 6);
    }

    [Fact]
    public void Two_year_growth_is_annualised()
    {
        // 2025 and 2026 are not leap years, so these dates are exactly 730 days apart.
        var rate = Xirr.Compute([
            (new DateTime(2025, 1, 1), -100d),
            (new DateTime(2027, 1, 1), 121d),
        ]);

        Assert.NotNull(rate);
        Assert.Equal(0.10, rate!.Value, precision: 6);
    }

    [Fact]
    public void Interim_contributions_are_weighted_by_time()
    {
        // Contributions of 100 at t0 and 100 at t0+1y, closing value 230 at t0+2y (365-day years, no leap day).
        var rate = Xirr.Compute([
            (new DateTime(2025, 1, 1), -100d),
            (new DateTime(2026, 1, 1), -100d),
            (new DateTime(2027, 1, 1), 230d),
        ]);

        Assert.NotNull(rate);
        // Verify the root directly rather than hard-coding a number.
        double Npv(double r) =>
            -100 - 100 / Math.Pow(1 + r, 1.0) + 230 / Math.Pow(1 + r, 2.0);
        Assert.True(Math.Abs(Npv(rate!.Value)) < 1e-6);
    }

    [Fact]
    public void No_solution_without_both_money_in_and_money_out()
    {
        Assert.Null(Xirr.Compute([(new DateTime(2025, 1, 1), -100d), (new DateTime(2026, 1, 1), -10d)]));
        Assert.Null(Xirr.Compute([(new DateTime(2025, 1, 1), -100d)]));
    }
}

public sealed class SalesTaxTests
{
    private static TaxableSale Sale(TaxCategory category, string date, decimal proceeds, decimal gain) =>
        new(category, DateTime.Parse(date), proceeds, gain);

    private static TaxMonth Month(IReadOnlyList<TaxMonth> months, TaxCategory category, string month) =>
        months.Single(m => m.Category == category.ToString() && m.Month == month);

    [Fact]
    public void Stock_sales_at_or_below_20000_are_exempt()
    {
        var months = SalesTaxCalculator.Compute([Sale(TaxCategory.Stock, "2026-01-10", 20_000m, 1_000m)]);

        var jan = Month(months, TaxCategory.Stock, "2026-01");
        Assert.True(jan.ExemptByThreshold);
        Assert.Equal(0m, jan.TaxDue);
        Assert.Null(jan.DarfDueDate);
    }

    [Fact]
    public void Stock_sales_above_20000_pay_15_percent_on_the_gain_and_have_a_darf_date()
    {
        var months = SalesTaxCalculator.Compute([Sale(TaxCategory.Stock, "2026-02-10", 30_000m, 3_000m)]);

        var feb = Month(months, TaxCategory.Stock, "2026-02");
        Assert.False(feb.ExemptByThreshold);
        Assert.Equal(450m, feb.TaxDue);
        Assert.Equal(new DateOnly(2026, 3, 31), feb.DarfDueDate); // Tuesday, last day of March
    }

    [Fact]
    public void Losses_offset_later_gains_in_the_same_category()
    {
        var months = SalesTaxCalculator.Compute([
            Sale(TaxCategory.Stock, "2026-01-10", 30_000m, -2_000m),
            Sale(TaxCategory.Stock, "2026-02-10", 30_000m, 5_000m),
        ]);

        Assert.Equal(0m, Month(months, TaxCategory.Stock, "2026-01").TaxDue);
        Assert.Equal(2_000m, Month(months, TaxCategory.Stock, "2026-01").LossCarriedOut);

        var feb = Month(months, TaxCategory.Stock, "2026-02");
        Assert.Equal(2_000m, feb.LossCarriedIn);
        Assert.Equal(3_000m, feb.TaxableBase);
        Assert.Equal(450m, feb.TaxDue);
        Assert.Equal(0m, feb.LossCarriedOut);
    }

    [Fact]
    public void A_loss_in_an_exempt_month_still_carries_forward()
    {
        var months = SalesTaxCalculator.Compute([
            Sale(TaxCategory.Stock, "2026-01-10", 10_000m, -1_000m),
            Sale(TaxCategory.Stock, "2026-02-10", 30_000m, 2_000m),
        ]);

        Assert.Equal(1_000m, Month(months, TaxCategory.Stock, "2026-02").LossCarriedIn);
        Assert.Equal(150m, Month(months, TaxCategory.Stock, "2026-02").TaxDue); // (2000 - 1000) * 15%
    }

    [Fact]
    public void Fii_is_taxed_at_20_percent_with_no_exemption()
    {
        var months = SalesTaxCalculator.Compute([Sale(TaxCategory.Fii, "2026-01-10", 1_000m, 1_000m)]);

        var jan = Month(months, TaxCategory.Fii, "2026-01");
        Assert.False(jan.ExemptByThreshold);
        Assert.Equal(200m, jan.TaxDue);
    }

    [Fact]
    public void Crypto_is_exempt_up_to_35000_and_taxed_above()
    {
        var months = SalesTaxCalculator.Compute([
            Sale(TaxCategory.Crypto, "2026-01-10", 30_000m, 2_000m),
            Sale(TaxCategory.Crypto, "2026-02-10", 50_000m, 2_000m),
        ]);

        Assert.Equal(0m, Month(months, TaxCategory.Crypto, "2026-01").TaxDue);
        Assert.Equal(300m, Month(months, TaxCategory.Crypto, "2026-02").TaxDue);
    }

    [Fact]
    public void Categories_do_not_offset_each_other()
    {
        var months = SalesTaxCalculator.Compute([
            Sale(TaxCategory.Stock, "2026-01-10", 30_000m, -5_000m),
            Sale(TaxCategory.Fii, "2026-01-12", 1_000m, 1_000m),
        ]);

        Assert.Equal(200m, Month(months, TaxCategory.Fii, "2026-01").TaxDue);
    }

    [Fact]
    public void Funds_are_reported_but_not_taxed_by_this_engine()
    {
        var months = SalesTaxCalculator.Compute([Sale(TaxCategory.Other, "2026-01-10", 5_000m, 800m)]);

        var other = Month(months, TaxCategory.Other, "2026-01");
        Assert.Null(other.RatePercent);
        Assert.Equal(0m, other.TaxDue);
        Assert.Equal(800m, other.NetGain);
    }

    [Theory]
    [InlineData("2026-01", "2026-02-27")] // Feb 28 2026 is a Saturday, so the 27th
    [InlineData("2026-11", "2026-12-31")] // Dec 31 2026 is a Thursday
    public void Darf_due_date_is_the_last_weekday_of_the_following_month(string month, string expected)
    {
        Assert.Equal(DateOnly.Parse(expected), SalesTaxCalculator.DarfDue(month));
    }
}
