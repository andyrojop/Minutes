using Microsoft.Data.SqlClient;
using Project_Minutes.Data;
using Project_Minutes.Models;

namespace Project_Minutes.Services;

public sealed class MeetingRepository(SqlDatabase db)
{
    public async Task<IReadOnlyList<MeetingRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            SELECT MeetingId, Title, MeetingDate, MeetingTime
            FROM Meetings
            ORDER BY MeetingDate DESC, MeetingTime DESC;
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<MeetingRecord>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new MeetingRecord
            {
                MeetingId = reader.GetInt32(0),
                Title = reader.IsDBNull(1) ? null : reader.GetString(1),
                MeetingDate = reader.GetDateTime(2),
                MeetingTime = reader.GetTimeSpan(3)
            });
        }

        return list;
    }

    public async Task<int> AddAsync(string? title, DateTime meetingDate, TimeSpan meetingTime,
        CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            INSERT INTO Meetings (Title, MeetingDate, MeetingTime)
            OUTPUT INSERTED.MeetingId
            VALUES (@Title, @MeetingDate, @MeetingTime);
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@Title", (object?)title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MeetingDate", meetingDate.Date);
        cmd.Parameters.AddWithValue("@MeetingTime", meetingTime);

        var id = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(id);
    }

    public async Task UpdateAsync(int meetingId, string? title, DateTime meetingDate, TimeSpan meetingTime,
        CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            UPDATE Meetings SET Title = @Title, MeetingDate = @MeetingDate, MeetingTime = @MeetingTime
            WHERE MeetingId = @MeetingId;
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@MeetingId", meetingId);
        cmd.Parameters.AddWithValue("@Title", (object?)title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MeetingDate", meetingDate.Date);
        cmd.Parameters.AddWithValue("@MeetingTime", meetingTime);

        var n = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (n == 0)
            throw new InvalidOperationException("No se encontró la reunión.");
    }

    public async Task DeleteAsync(int meetingId, CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = connection.BeginTransaction();

        try
        {
            const string sig = """
                DELETE FROM Signatures WHERE MinuteId IN (
                    SELECT MinuteId FROM Minutes WHERE MeetingId = @MeetingId);
                """;
            await using (var c1 = new SqlCommand(sig, connection, tx))
            {
                c1.CommandTimeout = db.CommandTimeoutSeconds;
                c1.Parameters.AddWithValue("@MeetingId", meetingId);
                await c1.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            const string taskSig = """
                DELETE FROM TaskSignatures WHERE TaskId IN (
                    SELECT TaskId FROM Tasks WHERE MinuteId IN (
                        SELECT MinuteId FROM Minutes WHERE MeetingId = @MeetingId));
                """;
            await using (var cTs = new SqlCommand(taskSig, connection, tx))
            {
                cTs.CommandTimeout = db.CommandTimeoutSeconds;
                cTs.Parameters.AddWithValue("@MeetingId", meetingId);
                await cTs.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            const string tasks = """
                DELETE FROM Tasks WHERE MinuteId IN (
                    SELECT MinuteId FROM Minutes WHERE MeetingId = @MeetingId);
                """;
            await using (var c2 = new SqlCommand(tasks, connection, tx))
            {
                c2.CommandTimeout = db.CommandTimeoutSeconds;
                c2.Parameters.AddWithValue("@MeetingId", meetingId);
                await c2.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            const string parts = "DELETE FROM Participants WHERE MeetingId = @MeetingId;";
            await using (var c3 = new SqlCommand(parts, connection, tx))
            {
                c3.CommandTimeout = db.CommandTimeoutSeconds;
                c3.Parameters.AddWithValue("@MeetingId", meetingId);
                await c3.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            const string mins = "DELETE FROM Minutes WHERE MeetingId = @MeetingId;";
            await using (var c4 = new SqlCommand(mins, connection, tx))
            {
                c4.CommandTimeout = db.CommandTimeoutSeconds;
                c4.Parameters.AddWithValue("@MeetingId", meetingId);
                await c4.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            const string meet = "DELETE FROM Meetings WHERE MeetingId = @MeetingId;";
            await using (var c5 = new SqlCommand(meet, connection, tx))
            {
                c5.CommandTimeout = db.CommandTimeoutSeconds;
                c5.Parameters.AddWithValue("@MeetingId", meetingId);
                var n = await c5.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                if (n == 0)
                    throw new InvalidOperationException("No se encontró la reunión.");
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
