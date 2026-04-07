using Microsoft.Data.SqlClient;
using Project_Minutes.Data;
using Project_Minutes.Models;

namespace Project_Minutes.Services;

public sealed class ParticipantRepository(SqlDatabase db)
{
    public async Task<IReadOnlyList<ParticipantRecord>> GetByMeetingAsync(int meetingId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            SELECT p.ParticipantId, p.MeetingId, p.UserId, u.Name, p.Position
            FROM Participants p
            INNER JOIN Users u ON u.UserId = p.UserId
            WHERE p.MeetingId = @MeetingId
            ORDER BY u.Name;
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@MeetingId", meetingId);

        var list = new List<ParticipantRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new ParticipantRecord
            {
                ParticipantId = reader.GetInt32(0),
                MeetingId = reader.GetInt32(1),
                UserId = reader.GetInt32(2),
                UserName = reader.GetString(3),
                Position = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        return list;
    }

    public async Task AddIfNotExistsAsync(int meetingId, int userId, string? position = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            IF NOT EXISTS (
                SELECT 1 FROM Participants WHERE MeetingId = @MeetingId AND UserId = @UserId)
            BEGIN
                INSERT INTO Participants (MeetingId, UserId, Position)
                VALUES (@MeetingId, @UserId, @Position);
            END
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@MeetingId", meetingId);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Position", (object?)position ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(int meetingId, int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = "DELETE FROM Participants WHERE MeetingId = @MeetingId AND UserId = @UserId;";
        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@MeetingId", meetingId);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
