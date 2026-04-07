using Microsoft.Data.SqlClient;
using Project_Minutes.Data;

namespace Project_Minutes.Services;

public sealed class TaskSignatureRepository(SqlDatabase db)
{
    public async Task UpsertAsync(int taskId, int userId, byte[] signaturePng,
        CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = connection.BeginTransaction();

        try
        {
            const string del = "DELETE FROM TaskSignatures WHERE TaskId = @TaskId;";
            await using (var c = new SqlCommand(del, connection, tx) { CommandTimeout = db.CommandTimeoutSeconds })
            {
                c.Parameters.AddWithValue("@TaskId", taskId);
                await c.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            const string ins = """
                INSERT INTO TaskSignatures (TaskId, UserId, SignatureImage)
                VALUES (@TaskId, @UserId, @SignatureImage);
                """;

            await using (var c = new SqlCommand(ins, connection, tx) { CommandTimeout = db.CommandTimeoutSeconds })
            {
                c.Parameters.AddWithValue("@TaskId", taskId);
                c.Parameters.AddWithValue("@UserId", userId);
                c.Parameters.Add("@SignatureImage", System.Data.SqlDbType.VarBinary, -1).Value = signaturePng;
                await c.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<byte[]?> GetPngAsync(int taskId, CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = "SELECT SignatureImage FROM TaskSignatures WHERE TaskId = @TaskId;";
        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@TaskId", taskId);
        var r = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (r is null || r is DBNull)
            return null;
        return (byte[])r;
    }

    public async Task DeleteForTaskAsync(int taskId, CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = "DELETE FROM TaskSignatures WHERE TaskId = @TaskId;";
        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@TaskId", taskId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
