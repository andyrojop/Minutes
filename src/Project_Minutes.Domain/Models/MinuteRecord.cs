namespace Project_Minutes.Models;

public sealed class MinuteRecord
{
    public int MinuteId { get; init; }
    public int MeetingId { get; init; }
    public string? MeetingTitle { get; init; }
    public string Content { get; init; } = "";
    public DateTime CreatedAt { get; init; }
}
