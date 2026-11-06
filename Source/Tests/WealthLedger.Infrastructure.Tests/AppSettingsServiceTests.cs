using Microsoft.Extensions.Logging.Abstractions;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Settings;
using WealthLedger.Infrastructure.Backup;
using WealthLedger.Infrastructure.Settings;
using Xunit;

namespace WealthLedger.Infrastructure.Tests;

public sealed class AppSettingsServiceTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly DatabaseLocation _location;

    public AppSettingsServiceTests() => _location = new DatabaseLocation(Path.Combine(_dir.Path, "test.db"));

    public void Dispose() => _dir.Dispose();

    private AppSettingsService Create() => new(_location, NullLogger<AppSettingsService>.Instance);

    [Fact]
    public async Task Defaults_to_local_provider_with_no_keys()
    {
        var view = await Create().GetViewAsync();
        Assert.Equal(AiProviders.Local, view.AiProvider);
        Assert.False(view.AllowCloudData);
        Assert.False(view.AnthropicKeyConfigured);
    }

    [Fact]
    public async Task Secret_round_trips_but_is_not_stored_in_plain_text()
    {
        var service = Create();
        await service.UpdateAsync(new AppSettingsUpdate(AiProvider: AiProviders.Anthropic, AnthropicApiKey: "sk-ant-secret-123"));

        Assert.Equal("sk-ant-secret-123", await Create().GetSecretAsync(AppSecret.AnthropicApiKey));
        Assert.DoesNotContain("sk-ant-secret-123", await File.ReadAllTextAsync(_location.AppSettingsPath));
        var view = await Create().GetViewAsync();
        Assert.True(view.AnthropicKeyConfigured);
        Assert.Equal(AiProviders.Anthropic, view.AiProvider);
    }

    [Fact]
    public async Task Null_keeps_secret_and_empty_string_clears_it()
    {
        var service = Create();
        await service.UpdateAsync(new AppSettingsUpdate(OpenAiApiKey: "sk-openai"));
        await service.UpdateAsync(new AppSettingsUpdate(AllowCloudData: true));
        Assert.Equal("sk-openai", await service.GetSecretAsync(AppSecret.OpenAiApiKey));

        await service.UpdateAsync(new AppSettingsUpdate(OpenAiApiKey: ""));
        Assert.Null(await service.GetSecretAsync(AppSecret.OpenAiApiKey));
        Assert.True((await service.GetViewAsync()).AllowCloudData);
    }

    [Fact]
    public async Task Unknown_provider_is_rejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Create().UpdateAsync(new AppSettingsUpdate(AiProvider: "skynet")));
    }

    [Fact]
    public async Task Each_provider_keeps_its_own_model()
    {
        var service = Create();
        await service.UpdateAsync(new AppSettingsUpdate(AiProvider: AiProviders.Anthropic, AiModel: "claude-sonnet-5-5"));
        await service.UpdateAsync(new AppSettingsUpdate(AiProvider: AiProviders.Ollama, AiModel: "qwen3:8b"));

        var view = await Create().GetViewAsync();
        Assert.Equal("qwen3:8b", view.AiModels[AiProviders.Ollama]);
        Assert.Equal("claude-sonnet-5-5", view.AiModels[AiProviders.Anthropic]);
    }

    [Fact]
    public async Task Blank_model_removes_only_that_providers_model()
    {
        var service = Create();
        await service.UpdateAsync(new AppSettingsUpdate(AiProvider: AiProviders.Anthropic, AiModel: "claude-opus-5-5"));
        await service.UpdateAsync(new AppSettingsUpdate(AiProvider: AiProviders.OpenAi, AiModel: "gpt-4o"));
        await service.UpdateAsync(new AppSettingsUpdate(AiProvider: AiProviders.OpenAi, AiModel: "  "));

        var view = await Create().GetViewAsync();
        Assert.False(view.AiModels.ContainsKey(AiProviders.OpenAi));
        Assert.Equal("claude-opus-5-5", view.AiModels[AiProviders.Anthropic]);
    }

    [Fact]
    public async Task Legacy_single_model_is_migrated_to_the_saved_provider()
    {
        await File.WriteAllTextAsync(_location.AppSettingsPath,
            """{"AiProvider":"ollama","AiModel":"llama3.1:8b","AllowCloudData":false,"Secrets":{},"OllamaBaseUrl":"http://localhost:11434"}""");

        var view = await Create().GetViewAsync();
        Assert.Equal("llama3.1:8b", view.AiModels[AiProviders.Ollama]);
    }

    [Fact]
    public async Task Corrupt_file_falls_back_to_defaults()
    {
        await File.WriteAllTextAsync(_location.AppSettingsPath, "{ not json");
        Assert.Equal(AiProviders.Local, (await Create().GetViewAsync()).AiProvider);
    }
}
