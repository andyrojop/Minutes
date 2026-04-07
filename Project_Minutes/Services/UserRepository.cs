using Microsoft.Data.SqlClient;
using Project_Minutes.Data;
using Project_Minutes.Models;

namespace Project_Minutes.Services;

public sealed class UserRepository(SqlDatabase db)
{
    public async Task<IReadOnlyList<UserRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            SELECT UserId, Name, Email
            FROM Users
            ORDER BY Name;
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<UserRecord>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new UserRecord
            {
                UserId = reader.GetInt32(0),
                Name = reader.GetString(1),
                Email = reader.IsDBNull(2) ? null : reader.GetString(2)
            });
        }

        return list;
    }

    public async Task<int> AddAsync(string name, string? email, CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            INSERT INTO Users (Name, Email)
            OUTPUT INSERTED.UserId
            VALUES (@Name, @Email);
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@Name", name);
        cmd.Parameters.AddWithValue("@Email", (object?)email ?? DBNull.Value);

        var id = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(id);
    }
}
