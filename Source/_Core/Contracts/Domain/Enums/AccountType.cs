namespace WealthLedger.Contracts.Domain.Enums;

public enum AccountType
{
    // Fixed-income / cash accounts
    CheckingAccount = 1,
    SavingsBox = 2,
    FixedTerm = 3,

    // Variable-income (renda variável)
    Stock = 4,
    FII = 5,
    ETF = 6,
    InvestmentFund = 7,
    BDR = 8,
    Crypto = 9
}
