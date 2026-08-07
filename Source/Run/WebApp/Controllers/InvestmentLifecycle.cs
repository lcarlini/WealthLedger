using WealthLedger.Infrastructure.Services;

namespace WealthLedger.WebApp.Controllers;

// Partial helpers for investment create/update — see InvestmentController.cs
internal static class InvestmentLifecycle
{
    public static void ApplyOnCreate(WealthLedger.Contracts.Domain.Investment domain, WealthLedger.Contracts.Api.Requests.InvestmentRequestBody body)
    {
        domain.Status = WealthLedger.Contracts.Domain.Enums.InvestmentStatus.Active;
        domain.StartDate = (body.StartDate ?? DateTime.UtcNow).Date;
        domain.PrincipalAmount = body.PrincipalAmount is > 0 ? body.PrincipalAmount.Value : body.Amount;
        if (domain.AccountType == WealthLedger.Contracts.Domain.Enums.AccountType.SavingsBox)
            domain.IsIncomeTaxExempt = true;
        else
            domain.IsIncomeTaxExempt = body.IsIncomeTaxExempt;
        InvestmentService.NormalizeLifecycleFields(domain);
    }

    public static void ApplyOnUpdate(
        WealthLedger.Contracts.Domain.Investment domain,
        WealthLedger.Contracts.Domain.Investment existing,
        WealthLedger.Contracts.Api.Requests.InvestmentRequestBody body)
    {
        domain.Status = existing.Status;
        domain.ArchivedAt = existing.ArchivedAt;
        domain.RedeemedAmount = existing.RedeemedAmount;
        domain.ArchiveNotes = existing.ArchiveNotes;
        domain.BookAmountAtArchive = existing.BookAmountAtArchive;
        domain.ExpectedTheoreticalAtArchive = existing.ExpectedTheoreticalAtArchive;
        domain.StartDate = body.StartDate?.Date ?? existing.StartDate;
        domain.PrincipalAmount = body.PrincipalAmount is > 0
            ? body.PrincipalAmount.Value
            : (existing.PrincipalAmount > 0 ? existing.PrincipalAmount : body.Amount);
        domain.IsIncomeTaxExempt = body.IsIncomeTaxExempt
            || domain.AccountType == WealthLedger.Contracts.Domain.Enums.AccountType.SavingsBox;
        InvestmentService.NormalizeLifecycleFields(domain, existing);
    }
}
