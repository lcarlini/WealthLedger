using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;

namespace WealthLedger.Application.Services;

public interface ICashFlowScheduleService
{
    Task<IEnumerable<CashFlowScheduleItem>> GetAllAsync();
    Task<CashFlowScheduleItem?> GetAsync(Guid id);
    Task<CashFlowScheduleItem> UpsertAsync(CashFlowScheduleItem entity);
    Task DeleteAsync(Guid id);
    Task<SimulationMatrixResponse> GetSimulationMatrixAsync(int fromYear, int fromMonth, int monthCount, decimal startingBalance = 0);
    Task<IEnumerable<ProposedCardInstallmentResponse>> GetProposedCardInstallmentsAsync();
    Task<CashFlowScheduleItem?> AddFromCardInstallmentAsync(Guid bankTransactionId);
    Task<int> AddAllFromCardInstallmentsAsync();
    Task<int> AddAllFromCardConsolidatedAsync();
}
