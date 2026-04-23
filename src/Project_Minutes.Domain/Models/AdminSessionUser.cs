namespace Project_Minutes.Models;

public sealed class AdminSessionUser
{
    public required int UserId { get; init; }
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
}
