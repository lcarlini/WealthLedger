using Microsoft.Extensions.Logging.Abstractions;
using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Advisor;
using WealthLedger.Contracts.Api.Queries;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Api.Settings;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Pagination;
using WealthLedger.Infrastructure.Advisor;
using WealthLedger.Infrastructure.Backup;
using WealthLedger.Infrastructure.Settings;
using Xunit;

namespace WealthLedger.Infrastructure.Tests;

public sealed class AiAdvisorTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private sealed class FakePortfolio : IPortfolioService
    {
        public Task<PortfolioOverviewResponse> GetOverviewAsync(int projectionYears = 3, CancellationToken cancellationToken = default)
            => Task.FromResult(new PortfolioOverviewResponse
            {
                TotalNetWorthBrl = 100_000m,
                InvestmentCount = 2,
                AllocationByType = [new AllocationSlice { Label = "FixedTerm", Percent = 70m }],
                // These must never reach the model:
                Holdings = [new HoldingPerformanceItem { Name = "SECRET-HOLDING-NAME" }],
                AllocationByInstitution = [new AllocationSlice { Label = "SECRET-BANK-NAME", Percent = 100m }],
            });

        public Task<PortfolioSnapshot> CaptureSnapshotAsync(string? notes = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<PortfolioSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<CalendarEventItem>> GetCalendarAsync(int monthsAhead = 12, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeTransactions(IEnumerable<BankTransaction> items) : IRepository<BankTransaction>
    {
        public Task<IEnumerable<BankTransaction>> GetAllAsync() => Task.FromResult(items);
        public Task<PagedResponse<BankTransaction>> GetPagedAsync(QueryOptions query) => throw new NotSupportedException();
        public Task<BankTransaction?> GetAsync(Guid id) => throw new NotSupportedException();
        public Task<BankTransaction> UpsertAsync(BankTransaction entity) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string fieldName, string fieldValue, Guid? excludeEntityId = null) => throw new NotSupportedException();
    }

    private (AiAdvisorService advisor, AppSettingsService settings) Create()
    {
        var settings = new AppSettingsService(new DatabaseLocation(Path.Combine(_dir.Path, "t.db")), NullLogger<AppSettingsService>.Instance);
        var tx = new FakeTransactions([
            new BankTransaction { Amount = -300m, Date = DateTime.UtcNow.AddDays(-5), Category = "Food", Description = "PIX JOAO DA SILVA 123.456.789-00" },
            new BankTransaction { Amount = -100m, Date = DateTime.UtcNow.AddDays(-9), Category = "Transport", Description = "UBER TRIP SECRET-MERCHANT" },
            new BankTransaction { Amount = 5000m, Date = DateTime.UtcNow.AddDays(-2), Category = "Income", Description = "SALARY" },
        ]);
        var builder = new AdvisorContextBuilder(new FakePortfolio(), tx);
        return (new AiAdvisorService(settings, builder, new NoHttpFactory(), NullLogger<AiAdvisorService>.Instance), settings);
    }

    private sealed class NoHttpFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("No network in tests.");
    }

    [Fact]
    public async Task Context_contains_aggregates_but_no_names_or_descriptions()
    {
        var (advisor, _) = Create();
        var context = await advisor.GetContextAsync(AdvisorScopes.General, "en");

        Assert.Contains("100,000.00", context.DataSummary);
        Assert.Contains("FixedTerm 70%", context.DataSummary);
        Assert.Contains("Food", context.DataSummary);
        Assert.DoesNotContain("SECRET-HOLDING-NAME", context.DataSummary);
        Assert.DoesNotContain("SECRET-BANK-NAME", context.DataSummary);
        Assert.DoesNotContain("JOAO", context.DataSummary);
        Assert.DoesNotContain("123.456.789", context.DataSummary);
        Assert.DoesNotContain("SECRET-MERCHANT", context.DataSummary);
        Assert.DoesNotContain("SALARY", context.DataSummary);
    }

    [Fact]
    public async Task Expenses_scope_leaves_out_the_portfolio()
    {
        var (advisor, _) = Create();
        var context = await advisor.GetContextAsync(AdvisorScopes.Expenses, "pt");

        Assert.DoesNotContain("Net worth", context.DataSummary);
        Assert.Contains("SPENDING", context.DataSummary);
        Assert.Contains("Brazilian Portuguese", context.SystemPrompt);
    }

    [Fact]
    public async Task Unknown_scope_is_rejected()
    {
        var (advisor, _) = Create();
        await Assert.ThrowsAsync<ArgumentException>(() => advisor.GetContextAsync("secrets", "en"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_question_is_rejected(string question)
    {
        var (advisor, _) = Create();
        await Assert.ThrowsAsync<ArgumentException>(() => advisor.AskAsync(new AdvisorRequest(question)));
    }

    [Fact]
    public async Task Oversized_question_is_rejected()
    {
        var (advisor, _) = Create();
        await Assert.ThrowsAsync<ArgumentException>(() => advisor.AskAsync(new AdvisorRequest(new string('x', 2001))));
    }

    [Fact]
    public async Task Local_provider_cannot_be_used_from_the_server()
    {
        var (advisor, _) = Create();
        await Assert.ThrowsAsync<InvalidOperationException>(() => advisor.AskAsync(new AdvisorRequest("hi")));
    }

    [Fact]
    public async Task Cloud_provider_without_a_key_is_refused_before_any_request()
    {
        var (advisor, settings) = Create();
        await settings.UpdateAsync(new AppSettingsUpdate(AiProvider: AiProviders.OpenAi));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => advisor.AskAsync(new AdvisorRequest("hi")));
        Assert.Contains("API key", ex.Message);
    }
}
