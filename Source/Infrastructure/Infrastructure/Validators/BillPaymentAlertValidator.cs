using WealthLedger.Application.Validators;
using WealthLedger.Contracts.Api.Requests;
using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Infrastructure.Validators;

public class BillPaymentAlertValidator : IPayloadValidator<BillPaymentAlertRequestBody>
{
    public Task<ValidationResult> ValidateAsync(BillPaymentAlertRequestBody payload)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(payload.Name))
            result.AddError(nameof(payload.Name), "Name is required.");
        else if (payload.Name.Trim().Length > 200)
            result.AddError(nameof(payload.Name), "Name must be at most 200 characters.");

        if (payload.Amount < 0)
            result.AddError(nameof(payload.Amount), "Amount cannot be negative.");

        if (payload.DueDay < 1 || payload.DueDay > 31)
            result.AddError(nameof(payload.DueDay), "Due day must be between 1 and 31.");

        if (!Enum.IsDefined(typeof(BillRecurrence), payload.Recurrence))
            result.AddError(nameof(payload.Recurrence), "Invalid recurrence.");

        if (!Enum.IsDefined(typeof(Currency), payload.Currency))
            result.AddError(nameof(payload.Currency), "Invalid currency.");

        if (payload.StartMonth < 1 || payload.StartMonth > 12)
            result.AddError(nameof(payload.StartMonth), "Start month must be between 1 and 12.");

        if (payload.StartYear < 2000 || payload.StartYear > 2100)
            result.AddError(nameof(payload.StartYear), "Start year must be between 2000 and 2100.");

        if (payload.EndYear.HasValue || payload.EndMonth.HasValue)
        {
            if (!payload.EndYear.HasValue || !payload.EndMonth.HasValue)
            {
                result.AddError(nameof(payload.EndYear), "Both end year and end month are required when setting an end date.");
            }
            else
            {
                if (payload.EndMonth is < 1 or > 12)
                    result.AddError(nameof(payload.EndMonth), "End month must be between 1 and 12.");
                if (payload.EndYear < 2000 || payload.EndYear > 2100)
                    result.AddError(nameof(payload.EndYear), "End year must be between 2000 and 2100.");

                var start = payload.StartYear * 12 + payload.StartMonth;
                var end = payload.EndYear.Value * 12 + payload.EndMonth.Value;
                if (end < start)
                    result.AddError(nameof(payload.EndYear), "End date must be on or after the start date.");
            }
        }

        if (payload.Notes is { Length: > 500 })
            result.AddError(nameof(payload.Notes), "Notes must be at most 500 characters.");

        return Task.FromResult(result);
    }
}
