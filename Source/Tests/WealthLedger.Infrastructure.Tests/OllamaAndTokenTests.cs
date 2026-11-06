using System.Text.Json;
using WealthLedger.Infrastructure.Advisor;
using WealthLedger.Infrastructure.Settings;
using Xunit;

namespace WealthLedger.Infrastructure.Tests;

public class OllamaAndTokenTests
{
    [Theory]
    [InlineData("http://localhost:11434", "http://localhost:11434")]
    [InlineData("http://127.0.0.1:11434/", "http://127.0.0.1:11434")]
    [InlineData("  http://LOCALHOST:11434  ", "http://localhost:11434")]
    [InlineData("", "http://localhost:11434")]
    public void Local_addresses_are_accepted_and_normalized(string input, string expected)
        => Assert.Equal(expected, AppSettingsEndpoints.NormalizeOllamaUrl(input));

    [Theory]
    [InlineData("http://192.168.0.10:11434")]
    [InlineData("https://ollama.example.com")]
    [InlineData("http://10.0.0.5:11434")]
    public void Remote_addresses_are_rejected_so_figures_never_leave_the_machine(string input)
        => Assert.Throws<ArgumentException>(() => AppSettingsEndpoints.NormalizeOllamaUrl(input));

    [Theory]
    [InlineData("ftp://localhost")]
    [InlineData("not a url")]
    public void Non_web_addresses_are_rejected(string input)
        => Assert.Throws<ArgumentException>(() => AppSettingsEndpoints.NormalizeOllamaUrl(input));

    [Fact]
    public void Ollama_reply_is_read_and_reasoning_blocks_are_removed()
    {
        using var doc = JsonDocument.Parse("""{"message":{"role":"assistant","content":"<think>hmm</think>Keep 6 months of expenses."}}""");
        Assert.Equal("Keep 6 months of expenses.", AiAdvisorService.ParseOllamaReply(doc.RootElement));
    }

    [Fact]
    public void Ollama_reply_without_content_is_empty()
    {
        using var doc = JsonDocument.Parse("""{"error":"boom"}""");
        Assert.Equal(string.Empty, AiAdvisorService.ParseOllamaReply(doc.RootElement));
    }

    [Fact]
    public void Cloud_default_is_haiku_to_keep_token_spend_low()
        => Assert.Equal("claude-haiku-5-5", AiAdvisorService.DefaultAnthropicModel);
}
