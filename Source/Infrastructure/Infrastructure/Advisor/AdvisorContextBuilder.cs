using System.Globalization;
using System.Text;
using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Advisor;
using WealthLedger.Contracts.Domain;

namespace WealthLedger.Infrastructure.Advisor;

/// <summary>
/// Turns the user's data into a short, aggregated text summary and owns the system prompt.
/// Privacy rule: totals, percentages and categories only. Never holding names, institution names, account
/// identifiers or transaction descriptions.
/// </summary>
public sealed class AdvisorContextBuilder(
    IPortfolioService portfolio,
    IRepository<BankTransaction> transactions)
{
    private const int SpendingMonths = 3;
    private const int MaxCategories = 8;

    public static string BuildSystemPrompt(string language)
    {
        var reply = string.Equals(language, "pt", StringComparison.OrdinalIgnoreCase)
            ? "Brazilian Portuguese"
            : "English";

        return $"""
            You are the financial assistant inside WealthLedger, a personal finance app used mainly by people in Brazil.
            You help the user decide where to put money, how to manage spending and which option is better for them.

            How to answer:
            - Reply in {reply}. Be concrete and brief: a short recommendation first, then the reasoning, then the main risks.
            - Base figures on the user's summary below when it is present. Never invent numbers. If something you need
              is missing (income, goals, time horizon, emergency fund), say what is missing and ask for it.
            - Compare options on after-tax, after-inflation terms. Mention relevant Brazilian rules when they matter
              (regressive income tax on fixed income, IOF in the first 30 days, LCI/LCA exemption, FGC coverage limit,
              the R$20,000 monthly exemption on stock sales, 15% and 20% rates on gains).
            - Rates and rules change; say when something should be double-checked.
            - Offer decision support, not guarantees. Do not promise returns. For large or irreversible decisions
              (taxes, leverage, concentrated positions) suggest consulting a licensed professional.
            - Do not ask for or repeat passwords, account numbers or documents.
            """;
    }

    public async Task<string> BuildSummaryAsync(string scope, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Aggregated figures from the user's WealthLedger (BRL; no names or account details):");

        if (scope is AdvisorScopes.Portfolio or AdvisorScopes.General)
            await AppendPortfolioAsync(sb, ct);

        if (scope is AdvisorScopes.Expenses or AdvisorScopes.General)
            await AppendSpendingAsync(sb);

        return sb.ToString().TrimEnd();
    }

    private async Task AppendPortfolioAsync(StringBuilder sb, CancellationToken ct)
    {
        var o = await portfolio.GetOverviewAsync(cancellationToken: ct);

        sb.AppendLine();
        sb.AppendLine("PORTFOLIO");
        sb.AppendLine($"- Net worth: {Money(o.TotalNetWorthBrl)} across {o.InvestmentCount} holdings");
        sb.AppendLine($"- Cash {Money(o.CashAmountBrl)}, fixed income {Money(o.FixedIncomeAmountBrl)}, variable income {Money(o.VariableIncomeAmountBrl)}");
        sb.AppendLine($"- Unrealized gain: {Money(o.UnrealizedGainBrl)} ({Pct(o.UnrealizedGainPercent)}); passive income this year: {Money(o.RealizedPassiveIncomeYtdBrl)}");
        sb.AppendLine($"- Financial health score {o.Health.Score}/100 (liquidity {o.Health.LiquidityScore}, diversification {o.Health.DiversificationScore}, inflation hedge {o.Health.InflationHedgeScore}, emergency coverage {o.Health.EmergencyCoverageScore}, goals {o.Health.GoalProgressScore})");

        if (o.AllocationByType.Count > 0)
            sb.AppendLine("- Allocation by type: " + string.Join("; ", o.AllocationByType.Select(s => $"{s.Label} {Pct(s.Percent)}")));
        if (o.AllocationByCurrency.Count > 0)
            sb.AppendLine("- Allocation by currency: " + string.Join("; ", o.AllocationByCurrency.Select(s => $"{s.Label} {Pct(s.Percent)}")));
        if (o.Benchmarks.Count > 0)
            sb.AppendLine("- Estimated annual return vs benchmarks: " + string.Join("; ",
                o.Benchmarks.Select(b => $"{b.Name} {Pct(b.AnnualRatePercent)} vs portfolio {Pct(b.PortfolioEstimatedAnnualPercent)}")));
        if (o.RebalanceSuggestions.Count > 0)
            sb.AppendLine("- Allocation drift: " + string.Join("; ",
                o.RebalanceSuggestions.Select(r => $"{r.Category} {Pct(r.CurrentPercent)} (target {Pct(r.TargetPercent)})")));
    }

    private async Task AppendSpendingAsync(StringBuilder sb)
    {
        var since = DateTime.UtcNow.Date.AddMonths(-SpendingMonths);
        var expenses = (await transactions.GetAllAsync())
            .Where(t => t.Amount < 0 && t.Date >= since)
            .ToList();

        sb.AppendLine();
        sb.AppendLine($"SPENDING (imported statements, last {SpendingMonths} months)");
        if (expenses.Count == 0)
        {
            sb.AppendLine("- No imported expenses in this period.");
            return;
        }

        var total = expenses.Sum(t => -t.Amount);
        sb.AppendLine($"- Total spent {Money(total)}; monthly average {Money(total / SpendingMonths)}");

        var byCategory = expenses
            .GroupBy(t => t.Category ?? "Other")
            .Select(g => (Category: g.Key, Amount: g.Sum(t => -t.Amount)))
            .OrderByDescending(x => x.Amount)
            .Take(MaxCategories)
            .Select(x => $"{x.Category} {Money(x.Amount)} ({Pct(x.Amount / total * 100m)})");
        sb.AppendLine("- Top categories: " + string.Join("; ", byCategory));

        var installments = expenses.Count(t => t.InstallmentTotal is > 1);
        if (installments > 0)
            sb.AppendLine($"- {installments} of the transactions are installment payments");
    }

    private static string Money(decimal value) => "R$ " + value.ToString("N2", CultureInfo.InvariantCulture);

    private static string Pct(decimal value) => value.ToString("0.#", CultureInfo.InvariantCulture) + "%";
}
