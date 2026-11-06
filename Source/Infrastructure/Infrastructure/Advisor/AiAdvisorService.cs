using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Advisor;
using WealthLedger.Contracts.Api.Settings;
using WealthLedger.Infrastructure.Settings;

namespace WealthLedger.Infrastructure.Advisor;

/// <summary>
/// Routes a question to the provider chosen in Settings.
/// - Anthropic / OpenAI: cloud. Figures are attached only after the user's explicit opt-in.
/// - Ollama: a Llama model on this machine. Nothing leaves the computer, so figures are always attached.
/// - Local (browser): handled in the browser; the server only supplies the prompt context.
/// </summary>
public sealed class AiAdvisorService(
    IAppSettingsService settings,
    AdvisorContextBuilder contextBuilder,
    IHttpClientFactory httpClientFactory,
    ILogger<AiAdvisorService> logger) : IAiAdvisorService
{
    /// <summary>Haiku is the cheapest current Claude model that still answers these questions well.</summary>
    public const string DefaultAnthropicModel = "claude-haiku-5-5";
    public const string DefaultOpenAiModel = "gpt-4o-mini";
    public const string DefaultOllamaModel = "llama3.1:8b";

    /// <summary>Short answers keep token spend and local memory use small.</summary>
    private const int MaxOutputTokens = 1500;
    private const int OllamaContextTokens = 4096;

    private const int MaxQuestionLength = 2000;
    private const int MaxHistoryTurns = 6;
    private const int MaxTurnLength = 2000;

    public async Task<AdvisorContext> GetContextAsync(string scope, string language, CancellationToken ct = default)
    {
        ValidateScope(scope);
        return new AdvisorContext(
            AdvisorContextBuilder.BuildSystemPrompt(language),
            await contextBuilder.BuildSummaryAsync(scope, ct));
    }

    public async Task<AdvisorAnswer> AskAsync(AdvisorRequest request, CancellationToken ct = default)
    {
        var question = request.Question?.Trim();
        if (string.IsNullOrEmpty(question))
            throw new ArgumentException("The question is empty.", nameof(request));
        if (question.Length > MaxQuestionLength)
            throw new ArgumentException($"The question is too long (max {MaxQuestionLength} characters).", nameof(request));
        ValidateScope(request.Scope);

        var view = await settings.GetViewAsync(ct);
        if (view.AiProvider == AiProviders.Local)
            throw new InvalidOperationException("The local provider runs in the browser; choose a cloud provider or Ollama in Settings to use the server.");

        var usedData = view.AiProvider == AiProviders.Ollama || view.AllowCloudData;
        var model = view.AiModels.GetValueOrDefault(view.AiProvider);
        var system = AdvisorContextBuilder.BuildSystemPrompt(request.Language);
        system += usedData
            ? "\n\n" + await contextBuilder.BuildSummaryAsync(request.Scope, ct)
            : "\n\nThe user has not shared any personal figures with you. Answer in general terms and say which numbers would let you be more specific.";

        var history = (request.History ?? [])
            .Where(t => t.Role is "user" or "assistant" && !string.IsNullOrWhiteSpace(t.Content))
            .TakeLast(MaxHistoryTurns)
            .Select(t => new AdvisorTurn(t.Role, Truncate(t.Content, MaxTurnLength)))
            .ToList();

        try
        {
            return view.AiProvider switch
            {
                AiProviders.Anthropic => await AskAnthropicAsync(await RequireKeyAsync(AppSecret.AnthropicApiKey, ct), model, system, history, question, usedData, ct),
                AiProviders.OpenAi => await AskOpenAiAsync(await RequireKeyAsync(AppSecret.OpenAiApiKey, ct), model, system, history, question, usedData, ct),
                AiProviders.Ollama => await AskOllamaAsync(view.OllamaBaseUrl, model, system, history, question, ct),
                _ => throw new InvalidOperationException($"Unknown AI provider '{view.AiProvider}'."),
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or AnthropicApiException or JsonException)
        {
            if (ct.IsCancellationRequested) throw;
            // Log the failure but never the key or the user's data.
            logger.LogWarning(ex, "AI provider {Provider} request failed", view.AiProvider);
            throw new InvalidOperationException(DescribeFailure(view.AiProvider, ex), ex);
        }
    }

    /// <summary>Models installed in the local Ollama server, for the model picker. Empty when Ollama is not running.</summary>
    public async Task<IReadOnlyList<string>> ListOllamaModelsAsync(CancellationToken ct = default)
    {
        var view = await settings.GetViewAsync(ct);
        var endpoint = new Uri(new Uri(AppSettingsEndpoints.NormalizeOllamaUrl(view.OllamaBaseUrl)), "/api/tags");

        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);
        try
        {
            using var response = await client.GetAsync(endpoint, ct);
            if (!response.IsSuccessStatusCode) return [];

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
                return [];

            return models.EnumerateArray()
                .Where(m => m.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                .Select(m => m.GetProperty("name").GetString()!)
                .Where(name => name.Length > 0)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (ct.IsCancellationRequested) throw;
            // Not running is a normal state for a local server; the UI shows an empty list and a hint.
            logger.LogInformation(ex, "Ollama model list unavailable");
            return [];
        }
    }

    private async Task<string> RequireKeyAsync(AppSecret secret, CancellationToken ct)
    {
        var apiKey = await settings.GetSecretAsync(secret, ct);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("No API key is saved for the selected AI provider. Add one in Settings.");
        return apiKey;
    }

    private static async Task<AdvisorAnswer> AskAnthropicAsync(
        string apiKey, string? configuredModel, string system, List<AdvisorTurn> history, string question,
        bool usedData, CancellationToken ct)
    {
        var model = string.IsNullOrWhiteSpace(configuredModel) ? DefaultAnthropicModel : configuredModel;
        AnthropicClient client = new() { ApiKey = apiKey };

        var messages = new List<MessageParam>();
        foreach (var turn in history)
        {
            messages.Add(turn.Role == "user"
                ? new UserMessageParam { Content = turn.Content }
                : new AssistantMessageParam { Content = turn.Content });
        }
        messages.Add(new UserMessageParam { Content = question });

        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = model,
            MaxTokens = MaxOutputTokens,
            System = system,
            Messages = messages,
            OutputConfig = new OutputConfig { Effort = "low" },
        }, cancellationToken: ct);

        var text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(b => b.Text)).Trim();
        if (text.Length == 0)
        {
            // Safety classifiers can decline a request (stop_reason "refusal") and return no text.
            throw new InvalidOperationException("The AI provider returned no answer. Try rephrasing the question.");
        }

        return new AdvisorAnswer(text, AiProviders.Anthropic, model, usedData);
    }

    private async Task<AdvisorAnswer> AskOpenAiAsync(
        string apiKey, string? configuredModel, string system, List<AdvisorTurn> history, string question,
        bool usedData, CancellationToken ct)
    {
        var model = string.IsNullOrWhiteSpace(configuredModel) ? DefaultOpenAiModel : configuredModel;

        var messages = new List<object> { new { role = "system", content = system } };
        messages.AddRange(history.Select(t => (object)new { role = t.Role, content = t.Content }));
        messages.Add(new { role = "user", content = question });

        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(90);
        using var http = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = JsonContent.Create(new { model, messages, max_tokens = MaxOutputTokens }),
        };
        http.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await client.SendAsync(http, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"OpenAI returned {(int)response.StatusCode}", null, response.StatusCode);

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim();
        if (string.IsNullOrEmpty(text))
            throw new InvalidOperationException("The AI provider returned no answer. Try rephrasing the question.");

        return new AdvisorAnswer(text, AiProviders.OpenAi, model, usedData);
    }

    private async Task<AdvisorAnswer> AskOllamaAsync(
        string baseUrl, string? configuredModel, string system, List<AdvisorTurn> history, string question,
        CancellationToken ct)
    {
        var model = string.IsNullOrWhiteSpace(configuredModel) ? DefaultOllamaModel : configuredModel;
        var endpoint = new Uri(new Uri(AppSettingsEndpoints.NormalizeOllamaUrl(baseUrl)), "/api/chat");

        var messages = new List<object> { new { role = "system", content = system } };
        messages.AddRange(history.Select(t => (object)new { role = t.Role, content = t.Content }));
        messages.Add(new { role = "user", content = question });

        using var client = httpClientFactory.CreateClient();
        // A local 8B model on CPU/GPU can take a minute or two for a long answer.
        client.Timeout = TimeSpan.FromMinutes(5);
        using var http = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new
            {
                model,
                messages,
                stream = false,
                // Small context and short answers keep memory use and time down on a laptop GPU.
                options = new { num_ctx = OllamaContextTokens, num_predict = MaxOutputTokens, temperature = 0.3 },
            }),
        };

        using var response = await client.SendAsync(http, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException($"The model '{model}' is not installed in Ollama. Run: ollama pull {model}");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Ollama returned {(int)response.StatusCode}", null, response.StatusCode);

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var text = ParseOllamaReply(doc.RootElement);
        if (string.IsNullOrEmpty(text))
            throw new InvalidOperationException("The local model returned no answer. Try rephrasing the question.");

        return new AdvisorAnswer(text, AiProviders.Ollama, model, UsedUserData: true);
    }

    /// <summary>Extracts the assistant text from an Ollama /api/chat response and strips reasoning blocks.</summary>
    public static string ParseOllamaReply(JsonElement root)
    {
        var content = root.TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
            ? c.GetString() ?? string.Empty
            : string.Empty;
        return Regex.Replace(content, @"<think>[\s\S]*?</think>", "").Trim();
    }

    private static string DescribeFailure(string provider, Exception ex)
    {
        if (provider == AiProviders.Ollama)
        {
            return ex is TaskCanceledException
                ? "The local model took too long to answer. Try a shorter question or a smaller model."
                : "Ollama is not reachable. Start it (ollama serve) and check the address in Settings.";
        }

        var status = ex switch
        {
            HttpRequestException h => (int?)h.StatusCode,
            AnthropicApiException a => (int?)a.StatusCode,
            _ => null,
        };

        return status switch
        {
            401 or 403 => "The AI provider rejected the API key. Check it in Settings.",
            404 => "The AI provider does not know the selected model. Check the model name in Settings.",
            429 => "The AI provider is rate limiting requests or the account has no credit. Try again later.",
            >= 500 => "The AI provider is having problems right now. Try again later.",
            _ => ex is TaskCanceledException
                ? "The AI provider took too long to answer. Try again."
                : "The AI provider could not be reached.",
        };
    }

    private static void ValidateScope(string scope)
    {
        if (!AdvisorScopes.All.Contains(scope))
            throw new ArgumentException($"Unknown scope '{scope}'.", nameof(scope));
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
