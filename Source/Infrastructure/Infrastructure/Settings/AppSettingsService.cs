using System.Text.Json;
using Microsoft.Extensions.Logging;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Settings;
using WealthLedger.Infrastructure.Backup;

namespace WealthLedger.Infrastructure.Settings;

/// <summary>
/// JSON-file settings store (app-settings.json next to the database), written atomically.
/// Singleton: one gate serializes read-modify-write.
/// </summary>
public sealed class AppSettingsService : IAppSettingsService
{
    private const int MaxValueLength = 512;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly DatabaseLocation _location;
    private readonly ILogger<AppSettingsService> _logger;
    private readonly SecretProtector _protector;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AppSettingsService(DatabaseLocation location, ILogger<AppSettingsService> logger)
    {
        _location = location;
        _logger = logger;
        _protector = new SecretProtector(location.AppSettingsKeyPath);
    }

    private sealed class Stored
    {
        public string AiProvider { get; set; } = AiProviders.Local;
        public string? AiModel { get; set; }
        public bool AllowCloudData { get; set; }
        public Dictionary<string, string> Secrets { get; set; } = new();
    }

    public async Task<AppSettingsView> GetViewAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try { return ToView(await LoadAsync(ct)); }
        finally { _gate.Release(); }
    }

    public async Task<AppSettingsView> UpdateAsync(AppSettingsUpdate update, CancellationToken ct = default)
    {
        if (update.AiProvider is not null && !AiProviders.All.Contains(update.AiProvider))
            throw new ArgumentException($"Unknown AI provider '{update.AiProvider}'.", nameof(update));
        foreach (var value in new[] { update.AiModel, update.AnthropicApiKey, update.OpenAiApiKey, update.BrapiToken, update.FredApiKey })
        {
            if (value is { Length: > MaxValueLength })
                throw new ArgumentException("A value is too long.", nameof(update));
        }

        await _gate.WaitAsync(ct);
        try
        {
            var stored = await LoadAsync(ct);
            if (update.AiProvider is not null) stored.AiProvider = update.AiProvider;
            if (update.AiModel is not null) stored.AiModel = string.IsNullOrWhiteSpace(update.AiModel) ? null : update.AiModel.Trim();
            if (update.AllowCloudData is not null) stored.AllowCloudData = update.AllowCloudData.Value;

            SetSecret(stored, AppSecret.AnthropicApiKey, update.AnthropicApiKey);
            SetSecret(stored, AppSecret.OpenAiApiKey, update.OpenAiApiKey);
            SetSecret(stored, AppSecret.BrapiToken, update.BrapiToken);
            SetSecret(stored, AppSecret.FredApiKey, update.FredApiKey);

            Directory.CreateDirectory(_location.DataDirectory);
            var tmp = _location.AppSettingsPath + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(stored, JsonOptions), ct);
            File.Move(tmp, _location.AppSettingsPath, overwrite: true);
            return ToView(stored);
        }
        finally { _gate.Release(); }
    }

    public async Task<string?> GetSecretAsync(AppSecret secret, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var stored = await LoadAsync(ct);
            return stored.Secrets.TryGetValue(secret.ToString(), out var protectedValue)
                ? _protector.Unprotect(protectedValue)
                : null;
        }
        finally { _gate.Release(); }
    }

    private void SetSecret(Stored stored, AppSecret secret, string? value)
    {
        if (value is null) return;
        if (string.IsNullOrWhiteSpace(value)) stored.Secrets.Remove(secret.ToString());
        else stored.Secrets[secret.ToString()] = _protector.Protect(value.Trim());
    }

    private bool Has(Stored stored, AppSecret secret)
        => stored.Secrets.TryGetValue(secret.ToString(), out var v) && _protector.Unprotect(v) is not null;

    private AppSettingsView ToView(Stored s) => new(
        s.AiProvider, s.AiModel, s.AllowCloudData,
        Has(s, AppSecret.AnthropicApiKey), Has(s, AppSecret.OpenAiApiKey),
        Has(s, AppSecret.BrapiToken), Has(s, AppSecret.FredApiKey));

    private async Task<Stored> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_location.AppSettingsPath)) return new Stored();
        try
        {
            await using var stream = File.OpenRead(_location.AppSettingsPath);
            var loaded = await JsonSerializer.DeserializeAsync<Stored>(stream, JsonOptions, ct) ?? new Stored();
            if (!AiProviders.All.Contains(loaded.AiProvider)) loaded.AiProvider = AiProviders.Local;
            loaded.Secrets ??= new();
            return loaded;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogWarning(ex, "App settings file is unreadable; using defaults.");
            return new Stored();
        }
    }
}
