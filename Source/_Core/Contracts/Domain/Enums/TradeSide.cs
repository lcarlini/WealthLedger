namespace WealthLedger.Contracts.Domain.Enums;

public enum TradeSide
{
    Buy = 1,
    Sell = 2,

    /// <summary>Cash dividend per share. UnitPrice is the amount per share held on the date; Quantity is 0.</summary>
    Dividend = 3,

    /// <summary>Shares received for free (stock dividends and splits). Quantity is the shares added; cost is unchanged.</summary>
    Bonus = 4,
}
