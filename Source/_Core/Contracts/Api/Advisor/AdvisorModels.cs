namespace WealthLedger.Contracts.Api.Advisor;

/// <summary>What the question is about. Decides which aggregated figures are attached.</summary>
public static class AdvisorScopes
{
    public const string Portfolio = "portfolio";
    public const string Expenses = "expenses";
    public const string General = "general";

    public static readonly IReadOnlyList<string> All = [Portfolio, Expenses, General];
}

/// <summary>One earlier turn of the conversation. Role is "user" or "assistant".</summary>
public sealed record AdvisorTurn(string Role, string Content);

public sealed record AdvisorRequest(
    string Question,
    string Scope = AdvisorScopes.General,
    string Language = "en",
    IReadOnlyList<AdvisorTurn>? History = null);

/// <param name="UsedUserData">True when aggregated portfolio/spending figures were attached to the request.</param>
public sealed record AdvisorAnswer(string Answer, string Provider, string Model, bool UsedUserData);

/// <summary>
/// Prompt material for a model that runs outside the server (the in-browser model). The summary contains
/// aggregated figures only: no holding or institution names, no account numbers, no transaction descriptions.
/// </summary>
public sealed record AdvisorContext(string SystemPrompt, string DataSummary);
