namespace WealthLedger.Contracts.Api.Settings;

/// <summary>Supported AI back ends. "local" runs entirely in the browser (WebLLM) and sends nothing anywhere.</summary>
public static class AiProviders
{
    public const string Local = "local";
    public const string Anthropic = "anthropic";
    public const string OpenAi = "openai";

    public static readonly IReadOnlyList<string> All = [Local, Anthropic, OpenAi];
}

/// <summary>
/// What the Settings page sees. Secrets are NEVER returned, only whether each one is configured.
/// </summary>
public sealed record AppSettingsView(
    string AiProvider,
    string? AiModel,
    bool AllowCloudData,
    bool AnthropicKeyConfigured,
    bool OpenAiKeyConfigured,
    bool BrapiTokenConfigured,
    bool FredKeyConfigured);

/// <summary>
/// Partial update. For every field: null = leave unchanged. For secrets, an empty string clears the stored value.
/// </summary>
public sealed record AppSettingsUpdate(
    string? AiProvider = null,
    string? AiModel = null,
    bool? AllowCloudData = null,
    string? AnthropicApiKey = null,
    string? OpenAiApiKey = null,
    string? BrapiToken = null,
    string? FredApiKey = null);
