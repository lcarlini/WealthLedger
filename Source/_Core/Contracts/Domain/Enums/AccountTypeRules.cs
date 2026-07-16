namespace WealthLedger.Contracts.Domain.Enums;

/// <summary>
/// Shared classification helpers for <see cref="AccountType"/>.
/// </summary>
public static class AccountTypeRules
{
    public static bool IsVariableIncome(AccountType type) => type is
        AccountType.Stock or
        AccountType.FII or
        AccountType.ETF or
        AccountType.InvestmentFund or
        AccountType.BDR or
        AccountType.Crypto;

    public static bool IsFixedTerm(AccountType type) => type == AccountType.FixedTerm;

    public static bool AllowsMaturity(AccountType type) => IsFixedTerm(type);

    public static bool AllowsMonthlyMovement(AccountType type) => type is
        AccountType.CheckingAccount or
        AccountType.SavingsBox;

    /// <summary>
    /// Yield is deterministic (CDI % or fixed annual rate). Variable-income is mark-to-market.
    /// </summary>
    public static bool HasDeterministicYield(AccountType type) => !IsVariableIncome(type);
}
