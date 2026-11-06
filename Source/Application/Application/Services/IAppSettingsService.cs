using WealthLedger.Contracts.Api.Settings;

namespace WealthLedger.Application.Services;

/// <summary>Names of the secrets the settings store can hold.</summary>
public enum AppSecret
{
    AnthropicApiKey,
    OpenAiApiKey,
    BrapiToken,
    FredApiKey
}

/// <summary>
/// App-level configuration (AI provider, API keys, market-data tokens) stored outside the database
/// so it survives restores. Secrets are encrypted at rest and never leave the server.
/// </summary>
public interface IAppSettingsService
{
    Task<AppSettingsView> GetViewAsync(CancellationToken ct = default);

    /// <exception cref="ArgumentException">Unknown AI provider or oversized value.</exception>
    Task<AppSettingsView> UpdateAsync(AppSettingsUpdate update, CancellationToken ct = default);

    /// <summary>Server-side use only. Returns null when the secret is not configured.</summary>
    Task<string?> GetSecretAsync(AppSecret secret, CancellationToken ct = default);
}
