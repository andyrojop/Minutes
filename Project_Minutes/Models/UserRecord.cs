namespace Project_Minutes.Models;

public sealed class UserRecord
{
    public int UserId { get; init; }
    public string Name { get; init; } = "";
    public string? Email { get; init; }
}
