using System.Net;

namespace WealthLedger.Infrastructure.Settings;

/// <summary>
/// Validates where the app may talk to a local model server. Only this computer is allowed, so a saved address can
/// never send your financial figures to another machine.
/// </summary>
public static class AppSettingsEndpoints
{
    public const string DefaultOllamaUrl = "http://localhost:11434";

    /// <summary>Returns the normalized base URL (scheme and host, no path) or throws ArgumentException.</summary>
    public static string NormalizeOllamaUrl(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? DefaultOllamaUrl : value.Trim().TrimEnd('/');
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("The Ollama address must be a web address such as http://localhost:11434.", nameof(value));

        var loopback = string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(uri.Host, out var ip) && IPAddress.IsLoopback(ip));
        if (!loopback)
            throw new ArgumentException("For privacy, Ollama must run on this computer (localhost or 127.0.0.1).", nameof(value));

        return $"{uri.Scheme}://{uri.Host}:{(uri.IsDefaultPort ? (uri.Scheme == "https" ? 443 : 80) : uri.Port)}";
    }

    /// <summary>Like NormalizeOllamaUrl but never throws: stored values were validated on save.</summary>
    public static string NormalizeOllamaUrlOrDefault(string? value)
    {
        try { return NormalizeOllamaUrl(value); }
        catch (ArgumentException) { return DefaultOllamaUrl; }
    }
}
