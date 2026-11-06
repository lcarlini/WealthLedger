using System.Text.Json;
using WealthLedger.Infrastructure.Services;
using Xunit;

namespace WealthLedger.Infrastructure.Tests;

public class MarketToolsTests
{
    private const string Header = "Tipo Titulo;Data Vencimento;Data Base;Taxa Compra Manha;Taxa Venda Manha;PU Compra Manha;PU Venda Manha;PU Base Manha";

    [Fact]
    public void Treasury_parser_reads_only_the_latest_day_and_decimal_commas()
    {
        var csv = string.Join('\n',
            Header,
            "Tesouro Selic;01/03/2029;05/11/2026;0,06;0,07;20216,19;20201,19;20201,19",
            "Tesouro Prefixado;01/01/2031;05/11/2026;13,52;13,58;545,10;544,33;544,33",
            "Tesouro Selic;01/03/2029;04/11/2026;0,05;0,06;20100,00;20090,00;20090,00");

        var snapshot = MarketToolsService.ParseLatestTreasury(new StringReader(csv), DateTime.UtcNow);

        Assert.Equal(new DateTime(2026, 11, 5), snapshot.BaseDate);
        Assert.Equal(2, snapshot.Bonds.Count);
        var selic = snapshot.Bonds.Single(b => b.Type == "Tesouro Selic");
        Assert.Equal(0.06m, selic.BuyRatePercent);
        Assert.Equal(20216.19m, selic.BuyPrice);
        Assert.Equal(new DateTime(2029, 3, 1), selic.Maturity);
        Assert.False(snapshot.FromCache);
    }

    [Fact]
    public void Treasury_parser_rejects_an_unexpected_file()
    {
        Assert.Throws<FormatException>(() =>
            MarketToolsService.ParseLatestTreasury(new StringReader("<html>maintenance</html>"), DateTime.UtcNow));
    }

    [Fact]
    public void Treasury_parser_rejects_a_file_with_no_rows()
    {
        Assert.Throws<FormatException>(() =>
            MarketToolsService.ParseLatestTreasury(new StringReader(Header), DateTime.UtcNow));
    }

    [Fact]
    public void Yahoo_chart_parser_skips_null_closes_and_derives_changes()
    {
        const string json = """
        {"chart":{"result":[{"meta":{"currency":"BRL","regularMarketPrice":110.0,"chartPreviousClose":90.0},
        "indicators":{"quote":[{"close":[100.0,null,105.0,110.0]}]}}]}}
        """;

        using var doc = JsonDocument.Parse(json);
        var index = MarketToolsService.ParseYahooChart(doc.RootElement, "^BVSP", "Ibovespa");

        Assert.NotNull(index);
        Assert.Equal(3, index!.Points.Count);
        Assert.Equal(110m, index.Price);
        Assert.Equal(4.76m, index.DayChangePercent);      // 110 vs previous close 105
        Assert.Equal(10m, index.PeriodChangePercent);     // 110 vs first close 100
        Assert.Equal("BRL", index.Currency);
    }

    [Fact]
    public void Yahoo_chart_parser_returns_null_without_a_price()
    {
        using var doc = JsonDocument.Parse("""{"chart":{"result":[{"meta":{"currency":"BRL"},"indicators":{"quote":[{"close":[1.0]}]}}]}}""");
        Assert.Null(MarketToolsService.ParseYahooChart(doc.RootElement, "X", "X"));
    }
}
