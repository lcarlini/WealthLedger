using WealthLedger.Application.Validators;
using WealthLedger.Contracts.Api.Requests;

namespace WealthLedger.Infrastructure.Validators;

public class CashFlowScheduleItemValidator : IPayloadValidator<CashFlowScheduleItemRequestBody>
{
    public Task<ValidationResult> ValidateAsync(CashFlowScheduleItemRequestBody payload)
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(payload.Name))
            result.AddError(nameof(payload.Name), "Name is required.");
        if (payload.StartMonth < 1 || payload.StartMonth > 12)
            result.AddError(nameof(payload.StartMonth), "Start month must be between 1 and 12.");
        if (payload.StartYear < 2000 || payload.StartYear > 2100)
            result.AddError(nameof(payload.StartYear), "Start year must be between 2000 and 2100.");
        if (payload.NumberOfMonths < 1)
            result.AddError(nameof(payload.NumberOfMonths), "Number of months must be at least 1.");
        return Task.FromResult(result);
    }
}
