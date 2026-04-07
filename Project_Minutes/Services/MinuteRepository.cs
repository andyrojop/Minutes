using Microsoft.Data.SqlClient;
using Project_Minutes.Data;
using Project_Minutes.Models;

namespace Project_Minutes.Services;

public sealed class MinuteRepository(SqlDatabase db)
{
    public async Task<IReadOnlyList<MinuteRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            SELECT m.MinuteId, m.MeetingId, mt.Title, m.Content, m.CreatedAt
            FROM Minutes m
            INNER JOIN Meetings mt ON mt.MeetingId = m.MeetingId
            ORDER BY m.CreatedAt DESC;
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<MinuteRecord>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new MinuteRecord
            {
                MinuteId = reader.GetInt32(0),
                MeetingId = reader.GetInt32(1),
                MeetingTitle = reader.IsDBNull(2) ? null : reader.GetString(2),
                Content = reader.IsDBNull(3) ? "" : reader.GetString(3),
                CreatedAt = reader.GetDateTime(4)
            });
        }

        return list;
    }

    public async Task<int> AddAsync(int meetingId, string content, CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            INSERT INTO Minutes (MeetingId, Content)
            OUTPUT INSERTED.MinuteId
            VALUES (@MeetingId, @Content);
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@MeetingId", meetingId);
        cmd.Parameters.AddWithValue("@Content", content);

        var id = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(id);
    }

    public async Task UpdateAsync(int minuteId, string content, CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            UPDATE Minutes SET Content = @Content WHERE MinuteId = @MinuteId;
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@MinuteId", minuteId);
        cmd.Parameters.AddWithValue("@Content", content);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affected == 0)
            throw new InvalidOperationException("No se encontró la minuta indicada.");
    }

    /// <summary>Lista para la UI con filtro opcional por reunión y datos de firma.</summary>
    public async Task<IReadOnlyList<MinuteListItem>> GetListItemsAsync(int? filterMeetingId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            SELECT m.MinuteId, m.MeetingId, mt.Title, m.Content, m.CreatedAt,
                (SELECT COUNT(*) FROM Participants p WHERE p.MeetingId = m.MeetingId),
                (SELECT COUNT(*) FROM Signatures s WHERE s.MinuteId = m.MinuteId)
            FROM Minutes m
            INNER JOIN Meetings mt ON mt.MeetingId = m.MeetingId
            WHERE (@FilterMeetingId IS NULL OR m.MeetingId = @FilterMeetingId)
            ORDER BY m.CreatedAt DESC;
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@FilterMeetingId", (object?)filterMeetingId ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<MinuteListItem>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new MinuteListItem
            {
                MinuteId = reader.GetInt32(0),
                MeetingId = reader.GetInt32(1),
                MeetingTitle = reader.IsDBNull(2) ? null : reader.GetString(2),
                Content = reader.IsDBNull(3) ? "" : reader.GetString(3),
                CreatedAt = reader.GetDateTime(4),
                ParticipantCount = reader.GetInt32(5),
                SignatureCount = reader.GetInt32(6)
            });
        }

        return list;
    }

    public async Task DeleteAsync(int minuteId, CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = connection.BeginTransaction();

        try
        {
            const string sig = "DELETE FROM Signatures WHERE MinuteId = @MinuteId;";
            await using (var c1 = new SqlCommand(sig, connection, tx))
            {
                c1.CommandTimeout = db.CommandTimeoutSeconds;
                c1.Parameters.AddWithValue("@MinuteId", minuteId);
                await c1.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            const string taskSig = """
                DELETE FROM TaskSignatures WHERE TaskId IN (
                    SELECT TaskId FROM Tasks WHERE MinuteId = @MinuteId);
                """;
            await using (var cTs = new SqlCommand(taskSig, connection, tx))
            {
                cTs.CommandTimeout = db.CommandTimeoutSeconds;
                cTs.Parameters.AddWithValue("@MinuteId", minuteId);
                await cTs.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            const string tasks = "DELETE FROM Tasks WHERE MinuteId = @MinuteId;";
            await using (var c2 = new SqlCommand(tasks, connection, tx))
            {
                c2.CommandTimeout = db.CommandTimeoutSeconds;
                c2.Parameters.AddWithValue("@MinuteId", minuteId);
                await c2.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            const string mins = "DELETE FROM Minutes WHERE MinuteId = @MinuteId;";
            await using (var c3 = new SqlCommand(mins, connection, tx))
            {
                c3.CommandTimeout = db.CommandTimeoutSeconds;
                c3.Parameters.AddWithValue("@MinuteId", minuteId);
                var n = await c3.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                if (n == 0)
                    throw new InvalidOperationException("No se encontró la minuta.");
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
