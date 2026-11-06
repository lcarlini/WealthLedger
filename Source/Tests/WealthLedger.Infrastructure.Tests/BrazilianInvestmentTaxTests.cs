using WealthLedger.Infrastructure.Services;
using Xunit;

namespace WealthLedger.Infrastructure.Tests;

public class BrazilianInvestmentTaxTests
{
    [Theory]
    [InlineData(1, 96)]
    [InlineData(4, 86)]
    [InlineData(10, 66)]
    [InlineData(29, 3)]
    [InlineData(30, 0)]
    public void Iof_follows_official_table(int days, int expected)
        => Assert.Equal(expected, BrazilianInvestmentTax.IofRatePercent(days));

    [Fact]
    public void Loss_is_not_hidden_and_pays_no_tax()
    {
        var (iof, _, ir, _, net) = BrazilianInvestmentTax.ApplyTaxes(-50m, 100, false, true);
        Assert.Equal(0m, iof);
        Assert.Equal(0m, ir);
        Assert.Equal(-50m, net);
    }

    [Fact]
    public void Gain_after_two_years_pays_15_percent_ir()
    {
        var (_, _, ir, rate, net) = BrazilianInvestmentTax.ApplyTaxes(1000m, 800, false, false);
        Assert.Equal(15m, rate);
        Assert.Equal(150m, ir);
        Assert.Equal(850m, net);
    }
}
