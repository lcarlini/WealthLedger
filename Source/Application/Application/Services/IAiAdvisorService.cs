using WealthLedger.Contracts.Api.Advisor;

namespace WealthLedger.Application.Services;

/// <summary>
/// Decision support on investments and spending. Uses the provider chosen in Settings. The "local" provider runs in
/// the browser, so for it the server only supplies the prompt context.
/// </summary>
public interface IAiAdvisorService
{
    /// <summary>Asks the configured cloud provider.</summary>
    /// <exception cref="ArgumentException">Empty or oversized question, or an unknown scope.</exception>
    /// <exception cref="InvalidOperationException">No cloud provider selected, key missing, or the provider failed.</exception>
    Task<AdvisorAnswer> AskAsync(AdvisorRequest request, CancellationToken ct = default);

    /// <summary>
    /// System prompt plus aggregated figures for the requested scope. Meant for the in-browser model, where the data
    /// never leaves the user's machine.
    /// </summary>
    Task<AdvisorContext> GetContextAsync(string scope, string language, CancellationToken ct = default);
}
