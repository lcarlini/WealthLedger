using WealthLedger.Application.Repositories;
using WealthLedger.Application.Validators;
using WealthLedger.Contracts.Api.Requests;
using WealthLedger.Contracts.Domain;

namespace WealthLedger.Infrastructure.Validators;

public class FinancialInstitutionValidator : IPayloadValidator<FinancialInstitutionRequestBody>
{
    private readonly IRepository<FinancialInstitution> _repository;

    public FinancialInstitutionValidator(IRepository<FinancialInstitution> repository)
    {
        _repository = repository;
    }

    public async Task<ValidationResult> ValidateAsync(FinancialInstitutionRequestBody payload)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(payload.Name))
        {
            result.AddError(nameof(payload.Name), "Name is required.");
            return result;
        }

        if (payload.Name.Length > 200)
        {
            result.AddError(nameof(payload.Name), "Name must not exceed 200 characters.");
        }

        if (payload.Description?.Length > 1000)
        {
            result.AddError(nameof(payload.Description), "Description must not exceed 1000 characters.");
        }

        var isDuplicate = await _repository.ExistsAsync(
            nameof(FinancialInstitution.Name),
            payload.Name,
            payload.Id == Guid.Empty ? null : payload.Id);

        if (isDuplicate)
        {
            result.AddError(nameof(payload.Name), $"A financial institution with the name '{payload.Name}' already exists.");
        }

        return result;
    }
}
