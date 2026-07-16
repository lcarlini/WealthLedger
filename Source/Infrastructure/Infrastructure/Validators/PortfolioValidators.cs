using WealthLedger.Application.Validators;
using WealthLedger.Contracts.Api.Requests;

namespace WealthLedger.Infrastructure.Validators;

public class PassiveIncomeValidator : IPayloadValidator<PassiveIncomeRequestBody>
{
    public Task<ValidationResult> ValidateAsync(PassiveIncomeRequestBody payload)
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(payload.Name))
            result.AddError(nameof(payload.Name), "Name is required.");
        if (payload.Amount <= 0)
            result.AddError(nameof(payload.Amount), "Amount must be greater than 0.");
        if (payload.PaymentDate == default)
            result.AddError(nameof(payload.PaymentDate), "Payment date is required.");
        return Task.FromResult(result);
    }
}

public class InvestmentGoalValidator : IPayloadValidator<InvestmentGoalRequestBody>
{
    public Task<ValidationResult> ValidateAsync(InvestmentGoalRequestBody payload)
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(payload.Name))
            result.AddError(nameof(payload.Name), "Name is required.");
        if (payload.TargetAmount <= 0)
            result.AddError(nameof(payload.TargetAmount), "Target amount must be greater than 0.");
        if (payload.CurrentAmount < 0)
            result.AddError(nameof(payload.CurrentAmount), "Current amount cannot be negative.");
        return Task.FromResult(result);
    }
}

public class WatchlistItemValidator : IPayloadValidator<WatchlistItemRequestBody>
{
    public Task<ValidationResult> ValidateAsync(WatchlistItemRequestBody payload)
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(payload.Ticker))
            result.AddError(nameof(payload.Ticker), "Ticker is required.");
        if (string.IsNullOrWhiteSpace(payload.Name))
            result.AddError(nameof(payload.Name), "Name is required.");
        if (payload.Ticker is { Length: > 30 })
            result.AddError(nameof(payload.Ticker), "Ticker cannot exceed 30 characters.");
        return Task.FromResult(result);
    }
}
