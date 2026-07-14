using WealthLedger.Application.Repositories;
using WealthLedger.Application.Validators;
using WealthLedger.Contracts.Api.Requests;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Infrastructure.Validators;

public class InvestmentValidator : IPayloadValidator<InvestmentRequestBody>
{
    private readonly IRepository<FinancialInstitution> _institutionRepository;
    private readonly IRepository<Investment> _investmentRepository;

    public InvestmentValidator(
        IRepository<FinancialInstitution> institutionRepository,
        IRepository<Investment> investmentRepository)
    {
        _institutionRepository = institutionRepository;
        _investmentRepository = investmentRepository;
    }

    public async Task<ValidationResult> ValidateAsync(InvestmentRequestBody payload)
    {
        var result = new ValidationResult();

        Investment? existingInvestment = null;
        if (payload.Id != Guid.Empty)
            existingInvestment = await _investmentRepository.GetAsync(payload.Id);

        if (string.IsNullOrWhiteSpace(payload.Name))
            result.AddError(nameof(payload.Name), "Name is required.");

        if (payload.FinancialInstitutionId == Guid.Empty)
        {
            result.AddError(nameof(payload.FinancialInstitutionId), "Financial institution is required.");
        }
        else
        {
            var institution = await _institutionRepository.GetAsync(payload.FinancialInstitutionId);
            if (institution == null)
                result.AddError(nameof(payload.FinancialInstitutionId), "Financial institution not found.");
        }

        if (payload.Currency == Currency.BRL)
        {
            if (payload.CdiPercentage < 0)
                result.AddError(nameof(payload.CdiPercentage), "CDI percentage cannot be negative. Use 0 for no yield (e.g. checking account).");
        }
        else
        {
            if (payload.AnnualRatePercent is null)
                result.AddError(nameof(payload.AnnualRatePercent), "Annual rate is required for USD/EUR investments (use 0 for no yield).");
            else if (payload.AnnualRatePercent < 0)
                result.AddError(nameof(payload.AnnualRatePercent), "Annual rate cannot be negative.");
        }

        if (payload.Amount < 0)
            result.AddError(nameof(payload.Amount), "Amount must be zero or greater.");

        switch (payload.AccountType)
        {
            case AccountType.FixedTerm:
                if (payload.MaturityDate == null)
                    result.AddError(nameof(payload.MaturityDate), "Maturity date is required for fixed-term investments.");
                else if (existingInvestment == null && payload.MaturityDate <= DateTime.UtcNow)
                    result.AddError(nameof(payload.MaturityDate), "Maturity date must be in the future for new fixed-term investments.");
                break;

            case AccountType.CheckingAccount:
            case AccountType.SavingsBox:
                if (payload.MaturityDate != null)
                    result.AddError(nameof(payload.MaturityDate), "Maturity date is not allowed for this account type.");
                break;
        }

        if (payload.RequiresMonthlyMovement)
        {
            if (payload.MonthlyMovementAmount is null or <= 0)
                result.AddError(nameof(payload.MonthlyMovementAmount), "Monthly movement amount is required and must be greater than 0.");
        }

        return result;
    }
}
