namespace Project_Minutes.Models;

public sealed class MeetingRecord
{
    public int MeetingId { get; init; }
    public string? Title { get; init; }
    public DateTime MeetingDate { get; init; }
    public TimeSpan MeetingTime { get; init; }
}
