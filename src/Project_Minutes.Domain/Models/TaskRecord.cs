namespace Project_Minutes.Models;

public sealed class TaskRecord
{
    public int TaskId { get; init; }
    public int MinuteId { get; init; }
    public string Title { get; init; } = "";
    public int? ResponsibleUserId { get; init; }
    public string? ResponsibleName { get; init; }
    public DateTime? DueDate { get; init; }
    public string Status { get; init; } = "Pending";
    public bool HasResponsibleSignature { get; init; }
}
