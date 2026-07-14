namespace WealthLedger.Contracts.Api.Responses;

public class TaskItemResponse
{
    public Guid Id { get; set; }
    public Guid InvestmentId { get; set; }
    public string? InvestmentName { get; set; }
    public string? InstitutionName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DueYear { get; set; }
    public int DueMonth { get; set; }
    public int? DueDay { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedDate { get; set; }
    public decimal? RequiredAmount { get; set; }
    /// <summary>Investment currency (e.g. BRL, USD) for formatting RequiredAmount.</summary>
    public string? Currency { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class FutureTaskResponse
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? InvestmentName { get; set; }
    public string? InstitutionName { get; set; }
    public int DueYear { get; set; }
    public int DueMonth { get; set; }
    public int? DueDay { get; set; }
    public decimal? Amount { get; set; }
    /// <summary>Optional ISO currency for Amount (e.g. BRL, USD).</summary>
    public string? Currency { get; set; }
    public string TaskType { get; set; } = string.Empty; // "Maturity", "MonthlyMovement", "MonthlyTask"
}