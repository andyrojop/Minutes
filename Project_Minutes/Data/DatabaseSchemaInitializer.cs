using Microsoft.Data.SqlClient;

namespace Project_Minutes.Data;

/// <summary>Crea objetos de BD necesarios si faltan (evita error al no haber ejecutado el script SQL a mano).</summary>
public static class DatabaseSchemaInitializer
{
    public static async Task EnsureExtendedSchemaAsync(SqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandTimeout = 120;
            cmd.CommandText = """
                IF OBJECT_ID(N'dbo.TaskSignatures', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.TaskSignatures (
                        TaskSignatureId INT NOT NULL IDENTITY(1, 1) PRIMARY KEY,
                        TaskId          INT NOT NULL,
                        UserId          INT NOT NULL,
                        SignatureImage  VARBINARY(MAX) NOT NULL,
                        SignedAt        DATETIME NOT NULL CONSTRAINT DF_TaskSignatures_SignedAt DEFAULT (GETDATE()),
                        CONSTRAINT FK_TaskSignatures_Task FOREIGN KEY (TaskId) REFERENCES dbo.Tasks (TaskId) ON DELETE CASCADE,
                        CONSTRAINT FK_TaskSignatures_User FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
                        CONSTRAINT UQ_TaskSignatures_TaskId UNIQUE (TaskId)
                    );
                END
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandTimeout = 120;
            cmd.CommandText = """
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'UX_Signatures_Minute_User'
                      AND object_id = OBJECT_ID(N'dbo.Signatures'))
                BEGIN
                    CREATE UNIQUE NONCLUSTERED INDEX UX_Signatures_Minute_User
                    ON dbo.Signatures (MinuteId, UserId);
                END
                """;
            try
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqlException)
            {
                // P. ej. filas duplicadas (MinuteId, UserId): la app funciona sin índice único.
            }
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandTimeout = 120;
            cmd.CommandText = """
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'dbo.Participants') AND name = N'Position')
                    ALTER TABLE dbo.Participants ADD Position NVARCHAR(150) NULL;
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
