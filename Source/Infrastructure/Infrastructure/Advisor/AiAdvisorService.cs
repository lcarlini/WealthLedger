using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Advisor;
using WealthLedger.Contracts.Api.Settings;

namespace WealthLedger.Infrastructure.Advisor;

/// <summary>
/// Routes a question to the provider chosen in Settings. Portfolio and spending figures are attached only when the
/// user opted in to sending summarized data to the cloud; otherwise the model answers from the question alone.
/// </summary>
public sealed class AiAdvisorService(
    IAppSettingsService settings,
    AdvisorContextBuilder contextBuilder,
    IHttpClientFactory httpClientFactory,
    ILogger<AiAdvisorService> logger) : IAiAdvisorService
{
    public const string DefaultAnthropicModel = "claude-opus-5-5";
    public const string DefaultOpenAiModel = "gpt-4o-mini";

    private const int MaxQuestionLength = 2000;
    private const int MaxHistoryTurns = 10;
    private const int MaxTurnLength = 4000;
    private const int MaxOutputTokens = 16000;

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
            throw new InvalidOperationException("The local provider runs in the browser; choose a cloud provider in Settings to use the server.");

        var secret = view.AiProvider == AiProviders.Anthropic ? AppSecret.AnthropicApiKey : AppSecret.OpenAiApiKey;
        var apiKey = await settings.GetSecretAsync(secret, ct);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("No API key is saved for the selected AI provider. Add one in Settings.");

        var usedData = view.AllowCloudData;
        var system = AdvisorContextBuilder.BuildSystemPrompt(request.Language);
        if (usedData)
            system += "\n\n" + await contextBuilder.BuildSummaryAsync(request.Scope, ct);
        else
            system += "\n\nThe user has not shared any personal figures with you. Answer in general terms and say which numbers would let you be more specific.";

        var history = (request.History ?? [])
            .Where(t => t.Role is "user" or "assistant" && !string.IsNullOrWhiteSpace(t.Content))
            .TakeLast(MaxHistoryTurns)
            .Select(t => new AdvisorTurn(t.Role, Truncate(t.Content, MaxTurnLength)))
            .ToList();

        try
        {
            return view.AiProvider == AiProviders.Anthropic
                ? await AskAnthropicAsync(apiKey, view.AiModel, system, history, question, usedData, ct)
                : await AskOpenAiAsync(apiKey, view.AiModel, system, history, question, usedData, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or AnthropicApiException or JsonException)
        {
            if (ct.IsCancellationRequested) throw;
            // Log the failure but never the key or the user's data.
            logger.LogWarning(ex, "AI provider {Provider} request failed", view.AiProvider);
            throw new InvalidOperationException(DescribeFailure(ex), ex);
        }
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
            Content = JsonContent.Create(new { model, messages }),
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

    private static string DescribeFailure(Exception ex)
    {
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
