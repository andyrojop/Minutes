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

    /// <summary>Personas para participantes (sin cuenta de administrador).</summary>
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

    public async Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            SELECT COUNT(*)
            FROM Users
            WHERE Role = @Role
              AND PasswordHash IS NOT NULL
              AND Active = 1;
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@Role", UserRoles.Administrator);

        var n = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(n);
    }

    public async Task<AdminSessionUser> RegisterAdministratorAsync(string displayName, string? email, string username,
        string password, CancellationToken cancellationToken = default)
    {
        ValidateDisplayName(displayName);
        ValidateUsername(username);
        ValidatePassword(password);

        var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);

        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = connection.BeginTransaction();

        try
        {
            const string sql = """
                INSERT INTO Users (Name, Email, Username, PasswordHash, Role, Active, AuthRegisteredAt)
                OUTPUT INSERTED.UserId
                VALUES (@Name, @Email, @Username, @PasswordHash, @Role, 1, SYSUTCDATETIME());
                """;

            await using var cmd = new SqlCommand(sql, connection, tx) { CommandTimeout = db.CommandTimeoutSeconds };
            cmd.Parameters.AddWithValue("@Name", displayName.Trim());
            cmd.Parameters.AddWithValue("@Email", (object?)email?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Username", username.Trim());
            cmd.Parameters.AddWithValue("@PasswordHash", hash);
            cmd.Parameters.AddWithValue("@Role", UserRoles.Administrator);

            var idObj = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var id = Convert.ToInt32(idObj);

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new AdminSessionUser
            {
                UserId = id,
                Username = username.Trim(),
                DisplayName = displayName.Trim(),
                Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim()
            };
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Registra el primer administrador cuando aún no existe ninguno con contraseña.</summary>
    public async Task<AdminSessionUser> RegisterFirstAdministratorAsync(string displayName, string? email,
        string username, string password, CancellationToken cancellationToken = default)
    {
        var count = await CountActiveAdministratorsAsync(cancellationToken).ConfigureAwait(false);
        if (count > 0)
            throw new InvalidOperationException("Ya existe un administrador registrado. Use inicio de sesión.");

        return await RegisterAdministratorAsync(displayName, email, username, password, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AdminSessionUser?> LoginAdministratorAsync(string username, string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);

        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            SELECT UserId, Name, Email, PasswordHash, Role
            FROM Users
            WHERE LOWER(LTRIM(RTRIM(Username))) = LOWER(LTRIM(@Username))
              AND Active = 1;
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = db.CommandTimeoutSeconds };
        cmd.Parameters.AddWithValue("@Username", username.Trim());

        int userId;
        string name;
        string? email;
        string? role;

        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return null;

            userId = reader.GetInt32(0);
            name = reader.GetString(1);
            email = reader.IsDBNull(2) ? null : reader.GetString(2);
            if (reader.IsDBNull(3))
                return null;

            var storedHash = reader.GetString(3);
            role = reader.IsDBNull(4) ? null : reader.GetString(4);

            if (!string.Equals(role, UserRoles.Administrator, StringComparison.OrdinalIgnoreCase))
                return null;

            if (!BCrypt.Net.BCrypt.Verify(password, storedHash))
                return null;
        }

        const string upd = """
            UPDATE Users SET LastLoginAt = SYSUTCDATETIME() WHERE UserId = @UserId;
            """;
        await using (var u = new SqlCommand(upd, connection) { CommandTimeout = db.CommandTimeoutSeconds })
        {
            u.Parameters.AddWithValue("@UserId", userId);
            await u.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return new AdminSessionUser
        {
            UserId = userId,
            Username = username.Trim(),
            DisplayName = name,
            Email = email
        };
    }

    private static void ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length < 2)
            throw new ArgumentException("El nombre debe tener al menos 2 caracteres.", nameof(displayName));
    }

    private static void ValidateUsername(string username)
    {
        var u = username.Trim();
        if (u.Length < 3)
            throw new ArgumentException("El usuario debe tener al menos 3 caracteres.", nameof(username));
        if (u.Contains(' ', StringComparison.Ordinal))
            throw new ArgumentException("El usuario no debe contener espacios.", nameof(username));
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < 6)
            throw new ArgumentException("La contraseña debe tener al menos 6 caracteres.", nameof(password));
    }
}
