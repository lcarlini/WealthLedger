using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Domain;

namespace WealthLedger.Infrastructure.Services;

public class PassiveIncomeService : IPassiveIncomeService
{
    private readonly IRepository<PassiveIncome> _repository;

    public PassiveIncomeService(IRepository<PassiveIncome> repository) => _repository = repository;

    public Task<IEnumerable<PassiveIncome>> GetAllAsync() => _repository.GetAllAsync();
    public Task<PassiveIncome?> GetAsync(Guid id) => _repository.GetAsync(id);
    public Task<PassiveIncome> UpsertAsync(PassiveIncome entity) => _repository.UpsertAsync(entity);
    public Task DeleteAsync(Guid id) => _repository.DeleteAsync(id);
}

public class InvestmentGoalService : IInvestmentGoalService
{
    private readonly IRepository<InvestmentGoal> _repository;

    public InvestmentGoalService(IRepository<InvestmentGoal> repository) => _repository = repository;

    public Task<IEnumerable<InvestmentGoal>> GetAllAsync() => _repository.GetAllAsync();
    public Task<InvestmentGoal?> GetAsync(Guid id) => _repository.GetAsync(id);
    public Task<InvestmentGoal> UpsertAsync(InvestmentGoal entity) => _repository.UpsertAsync(entity);
    public Task DeleteAsync(Guid id) => _repository.DeleteAsync(id);
}

public class WatchlistService : IWatchlistService
{
    private readonly IRepository<WatchlistItem> _repository;

    public WatchlistService(IRepository<WatchlistItem> repository) => _repository = repository;

    public Task<IEnumerable<WatchlistItem>> GetAllAsync() => _repository.GetAllAsync();
    public Task<WatchlistItem?> GetAsync(Guid id) => _repository.GetAsync(id);
    public Task<WatchlistItem> UpsertAsync(WatchlistItem entity) => _repository.UpsertAsync(entity);
    public Task DeleteAsync(Guid id) => _repository.DeleteAsync(id);
}
