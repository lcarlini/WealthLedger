using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;

namespace WealthLedger.Application.Services;

public interface IBillPaymentAlertService
{
    Task<IEnumerable<BillPaymentAlert>> GetAllAsync();
    Task<BillPaymentAlert?> GetAsync(Guid id);
    Task<BillPaymentAlert> UpsertAsync(BillPaymentAlert entity);
    Task DeleteAsync(Guid id);

    Task<IEnumerable<BillPaymentOccurrenceResponse>> GetDueThisMonthAsync();
    Task<IEnumerable<BillPaymentOccurrenceResponse>> GetUpcomingAsync(int monthsAhead = 12);
    Task<IEnumerable<BillPaymentOccurrenceResponse>> GetPastAsync();
    Task<BillPaymentOccurrenceResponse?> MarkPaidAsync(Guid occurrenceId);
    Task<int> GetPendingCountAsync();
}
