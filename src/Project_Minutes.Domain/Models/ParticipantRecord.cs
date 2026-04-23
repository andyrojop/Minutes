namespace Project_Minutes.Models;

public sealed class ParticipantRecord
{
    public int ParticipantId { get; init; }
    public int MeetingId { get; init; }
    public int UserId { get; init; }
    public string UserName { get; init; } = "";
    public string? Position { get; init; }

    public string ListCaption => string.IsNullOrWhiteSpace(Position)
        ? UserName
        : $"{UserName}  ·  {Position}";
}
