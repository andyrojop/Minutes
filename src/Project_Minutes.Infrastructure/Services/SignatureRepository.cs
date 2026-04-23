using Microsoft.Data.SqlClient;
using Project_Minutes.Data;

namespace Project_Minutes.Services;

public sealed class SignatureRepository(SqlDatabase db)
{
    /// <summary>Una firma por persona y minuta (reemplaza la de ese usuario si existía).</summary>
    public async Task UpsertMinuteUserAsync(int minuteId, int userId, byte[] signaturePng,
        CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = connection.BeginTransaction();

        try
        {
            const string del = "DELETE FROM Signatures WHERE MinuteId = @MinuteId AND UserId = @UserId;";
            await using (var delCmd = new SqlCommand(del, connection, tx) { CommandTimeout = db.CommandTimeoutSeconds })
            {
                delCmd.Parameters.AddWithValue("@MinuteId", minuteId);
                delCmd.Parameters.AddWithValue("@UserId", userId);
                await delCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            const string ins = """
                INSERT INTO Signatures (MinuteId, UserId, SignatureImage)
                VALUES (@MinuteId, @UserId, @SignatureImage);
                """;

            await using (var insCmd = new SqlCommand(ins, connection, tx) { CommandTimeout = db.CommandTimeoutSeconds })
            {
                insCmd.Parameters.AddWithValue("@MinuteId", minuteId);
                insCmd.Parameters.AddWithValue("@UserId", userId);
                insCmd.Parameters.Add("@SignatureImage", System.Data.SqlDbType.VarBinary, -1).Value = signaturePng;
                await insCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<IReadOnlyDictionary<int, byte[]>> GetAllPngByUserForMinuteAsync(int minuteId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            SELECT UserId, SignatureImage FROM Signatures WHERE MinuteId = @MinuteId;
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@MinuteId", minuteId);

        var map = new Dictionary<int, byte[]>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            map[reader.GetInt32(0)] = (byte[])reader.GetValue(1);

        return map;
    }

    public async Task DeleteMinuteUserAsync(int minuteId, int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = "DELETE FROM Signatures WHERE MinuteId = @MinuteId AND UserId = @UserId;";
        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@MinuteId", minuteId);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAllForMinuteAsync(int minuteId, CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = "DELETE FROM Signatures WHERE MinuteId = @MinuteId;";
        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@MinuteId", minuteId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Mantiene compatibilidad con vistas que solo necesitan una imagen cualquiera.</summary>
    public Task<byte[]?> GetLatestPngForMinuteAsync(int minuteId, CancellationToken cancellationToken = default) =>
        GetAnyPngForMinuteAsync(minuteId, cancellationToken);

    private async Task<byte[]?> GetAnyPngForMinuteAsync(int minuteId, CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            SELECT TOP (1) SignatureImage
            FROM Signatures
            WHERE MinuteId = @MinuteId
            ORDER BY SignedAt DESC;
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@MinuteId", minuteId);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is null || result is DBNull)
            return null;

        return (byte[])result;
    }
}
