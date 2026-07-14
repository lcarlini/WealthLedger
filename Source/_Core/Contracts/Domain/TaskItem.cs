namespace WealthLedger.Contracts.Domain;

public class TaskItem : BaseEntity
{
    public Guid InvestmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DueYear { get; set; }
    public int DueMonth { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedDate { get; set; }
}
