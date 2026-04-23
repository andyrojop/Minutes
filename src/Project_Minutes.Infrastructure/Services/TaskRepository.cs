using Microsoft.Data.SqlClient;
using Project_Minutes.Data;
using Project_Minutes.Models;

namespace Project_Minutes.Services;

public sealed class TaskRepository(SqlDatabase db)
{
    public async Task<IReadOnlyList<TaskRecord>> GetByMinuteIdAsync(int minuteId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            SELECT t.TaskId, t.MinuteId, t.Title, t.ResponsibleUserId, u.Name, t.DueDate, t.Status,
                CASE WHEN ts.TaskSignatureId IS NOT NULL THEN 1 ELSE 0 END
            FROM Tasks t
            LEFT JOIN Users u ON u.UserId = t.ResponsibleUserId
            LEFT JOIN TaskSignatures ts ON ts.TaskId = t.TaskId
            WHERE t.MinuteId = @MinuteId
            ORDER BY t.DueDate, t.TaskId;
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@MinuteId", minuteId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<TaskRecord>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new TaskRecord
            {
                TaskId = reader.GetInt32(0),
                MinuteId = reader.GetInt32(1),
                Title = reader.GetString(2),
                ResponsibleUserId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                ResponsibleName = reader.IsDBNull(4) ? null : reader.GetString(4),
                DueDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                Status = reader.IsDBNull(6) ? "Pending" : reader.GetString(6),
                HasResponsibleSignature = !reader.IsDBNull(7) && reader.GetInt32(7) != 0
            });
        }

        return list;
    }

    public async Task<int> AddAsync(int minuteId, string title, int? responsibleUserId, DateTime? dueDate,
        CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            INSERT INTO Tasks (MinuteId, Title, ResponsibleUserId, DueDate)
            OUTPUT INSERTED.TaskId
            VALUES (@MinuteId, @Title, @ResponsibleUserId, @DueDate);
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@MinuteId", minuteId);
        cmd.Parameters.AddWithValue("@Title", title);
        cmd.Parameters.AddWithValue("@ResponsibleUserId", (object?)responsibleUserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DueDate", (object?)dueDate?.Date ?? DBNull.Value);

        var id = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(id);
    }

    public async Task DeleteAsync(int taskId, CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sig = "DELETE FROM TaskSignatures WHERE TaskId = @TaskId;";
        await using (var s = new SqlCommand(sig, connection) { CommandTimeout = db.CommandTimeoutSeconds })
        {
            s.Parameters.AddWithValue("@TaskId", taskId);
            await s.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        const string sql = "DELETE FROM Tasks WHERE TaskId = @TaskId;";
        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@TaskId", taskId);

        var n = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (n == 0)
            throw new InvalidOperationException("No se encontró el compromiso.");
    }
}
