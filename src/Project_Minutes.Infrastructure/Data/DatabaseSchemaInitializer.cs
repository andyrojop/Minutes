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

        await EnsureReviewModelSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tablas y columnas del modelo del code review (§3.2 pantallas + §3.5 datos): Personal, login/roles,
    /// reuniones extendidas, participantes por Personal, actas (estados/hash), adjuntos, firmantes, historial de tareas, auditoría.
    /// </summary>
    public static async Task EnsureReviewModelSchemaAsync(SqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandTimeout = 180;
            cmd.CommandText = """
                IF OBJECT_ID(N'dbo.Personal', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.Personal (
                        PersonalId      INT NOT NULL IDENTITY(1, 1) PRIMARY KEY,
                        FirstName       NVARCHAR(100) NOT NULL,
                        LastName        NVARCHAR(100) NOT NULL,
                        Position        NVARCHAR(150) NULL,
                        Area            NVARCHAR(150) NULL,
                        Email           NVARCHAR(256) NULL,
                        Phone           NVARCHAR(50)  NULL,
                        PhotoBlob       VARBINARY(MAX) NULL,
                        CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_Personal_CreatedAt DEFAULT (SYSUTCDATETIME())
                    );
                    CREATE INDEX IX_Personal_LastName_FirstName ON dbo.Personal (LastName, FirstName);
                END
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandTimeout = 180;
            cmd.CommandText = """
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'PersonalId')
                    ALTER TABLE dbo.Users ADD PersonalId INT NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'Username')
                    ALTER TABLE dbo.Users ADD Username NVARCHAR(100) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'PasswordHash')
                    ALTER TABLE dbo.Users ADD PasswordHash NVARCHAR(500) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'Role')
                    ALTER TABLE dbo.Users ADD Role NVARCHAR(50) NOT NULL CONSTRAINT DF_Users_Role DEFAULT (N'User');
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'Active')
                    ALTER TABLE dbo.Users ADD Active BIT NOT NULL CONSTRAINT DF_Users_Active DEFAULT (1);
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Users_Personal')
                    ALTER TABLE dbo.Users ADD CONSTRAINT FK_Users_Personal FOREIGN KEY (PersonalId) REFERENCES dbo.Personal (PersonalId);
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandTimeout = 180;
            cmd.CommandText = """
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Users_Username' AND object_id = OBJECT_ID(N'dbo.Users'))
                BEGIN
                    CREATE UNIQUE INDEX UX_Users_Username ON dbo.Users (Username) WHERE Username IS NOT NULL;
                END
                """;
            try
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqlException)
            {
                // Duplicados de Username: omitir índice único.
            }
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandTimeout = 180;
            cmd.CommandText = """
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'AuthRegisteredAt')
                    ALTER TABLE dbo.Users ADD AuthRegisteredAt DATETIME2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'LastLoginAt')
                    ALTER TABLE dbo.Users ADD LastLoginAt DATETIME2 NULL;
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandTimeout = 180;
            cmd.CommandText = """
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Meetings') AND name = N'Location')
                    ALTER TABLE dbo.Meetings ADD Location NVARCHAR(300) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Meetings') AND name = N'OrganizerId')
                    ALTER TABLE dbo.Meetings ADD OrganizerId INT NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Meetings') AND name = N'MeetingStatus')
                    ALTER TABLE dbo.Meetings ADD MeetingStatus NVARCHAR(50) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Meetings') AND name = N'CreatedBy')
                    ALTER TABLE dbo.Meetings ADD CreatedBy INT NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Meetings') AND name = N'RecordCreatedAt')
                    ALTER TABLE dbo.Meetings ADD RecordCreatedAt DATETIME2 NULL CONSTRAINT DF_Meetings_RecordCreatedAt DEFAULT (SYSUTCDATETIME());
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Meetings') AND name = N'StartsAt')
                    ALTER TABLE dbo.Meetings ADD StartsAt DATETIME2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Meetings') AND name = N'EndsAt')
                    ALTER TABLE dbo.Meetings ADD EndsAt DATETIME2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Meetings_Organizer')
                    ALTER TABLE dbo.Meetings ADD CONSTRAINT FK_Meetings_Organizer FOREIGN KEY (OrganizerId) REFERENCES dbo.Users (UserId);
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Meetings_CreatedBy')
                    ALTER TABLE dbo.Meetings ADD CONSTRAINT FK_Meetings_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users (UserId);
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandTimeout = 180;
            cmd.CommandText = """
                IF OBJECT_ID(N'dbo.MeetingParticipants', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.MeetingParticipants (
                        MeetingId       INT NOT NULL,
                        PersonalId      INT NOT NULL,
                        RoleInMeeting   NVARCHAR(100) NULL,
                        CONSTRAINT PK_MeetingParticipants PRIMARY KEY (MeetingId, PersonalId),
                        CONSTRAINT FK_MP_Meeting FOREIGN KEY (MeetingId) REFERENCES dbo.Meetings (MeetingId) ON DELETE CASCADE,
                        CONSTRAINT FK_MP_Personal FOREIGN KEY (PersonalId) REFERENCES dbo.Personal (PersonalId)
                    );
                END
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandTimeout = 180;
            cmd.CommandText = """
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Minutes') AND name = N'MinuteStatus')
                    ALTER TABLE dbo.Minutes ADD MinuteStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_Minutes_MinuteStatus DEFAULT (N'Draft');
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Minutes') AND name = N'AuthorUserId')
                    ALTER TABLE dbo.Minutes ADD AuthorUserId INT NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Minutes') AND name = N'LockedAt')
                    ALTER TABLE dbo.Minutes ADD LockedAt DATETIME2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Minutes') AND name = N'ContentHash')
                    ALTER TABLE dbo.Minutes ADD ContentHash NVARCHAR(128) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Minutes_Author')
                    ALTER TABLE dbo.Minutes ADD CONSTRAINT FK_Minutes_Author FOREIGN KEY (AuthorUserId) REFERENCES dbo.Users (UserId);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Minutes_MinuteStatus' AND object_id = OBJECT_ID(N'dbo.Minutes'))
                    CREATE INDEX IX_Minutes_MinuteStatus ON dbo.Minutes (MinuteStatus);
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandTimeout = 180;
            cmd.CommandText = """
                IF OBJECT_ID(N'dbo.MinuteAttachments', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.MinuteAttachments (
                        AttachmentId    INT NOT NULL IDENTITY(1, 1) PRIMARY KEY,
                        MinuteId        INT NOT NULL,
                        FileName        NVARCHAR(400) NOT NULL,
                        MimeType        NVARCHAR(200) NULL,
                        StoragePath     NVARCHAR(2000) NULL,
                        FileContent     VARBINARY(MAX) NULL,
                        UploadedAt      DATETIME2 NOT NULL CONSTRAINT DF_MinuteAttachments_UploadedAt DEFAULT (SYSUTCDATETIME()),
                        CONSTRAINT FK_MinuteAttachments_Minute FOREIGN KEY (MinuteId) REFERENCES dbo.Minutes (MinuteId) ON DELETE CASCADE
                    );
                    CREATE INDEX IX_MinuteAttachments_MinuteId ON dbo.MinuteAttachments (MinuteId);
                END
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandTimeout = 180;
            cmd.CommandText = """
                IF OBJECT_ID(N'dbo.MinuteSigners', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.MinuteSigners (
                        MinuteSignerId  INT NOT NULL IDENTITY(1, 1) PRIMARY KEY,
                        MinuteId        INT NOT NULL,
                        PersonalId      INT NOT NULL,
                        SignOrder       INT NOT NULL CONSTRAINT DF_MinuteSigners_Order DEFAULT (0),
                        SignedAt        DATETIME2 NULL,
                        SignatureImage  VARBINARY(MAX) NULL,
                        SignatureHash   NVARCHAR(128) NULL,
                        CONSTRAINT FK_MinuteSigners_Minute FOREIGN KEY (MinuteId) REFERENCES dbo.Minutes (MinuteId) ON DELETE CASCADE,
                        CONSTRAINT FK_MinuteSigners_Personal FOREIGN KEY (PersonalId) REFERENCES dbo.Personal (PersonalId),
                        CONSTRAINT UQ_MinuteSigners_Minute_Personal UNIQUE (MinuteId, PersonalId)
                    );
                    CREATE INDEX IX_MinuteSigners_MinuteId ON dbo.MinuteSigners (MinuteId);
                END
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandTimeout = 180;
            cmd.CommandText = """
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Tasks') AND name = N'AssigneePersonalId')
                    ALTER TABLE dbo.Tasks ADD AssigneePersonalId INT NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Tasks') AND name = N'CompletedAt')
                    ALTER TABLE dbo.Tasks ADD CompletedAt DATETIME2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Tasks_AssigneePersonal')
                    ALTER TABLE dbo.Tasks ADD CONSTRAINT FK_Tasks_AssigneePersonal FOREIGN KEY (AssigneePersonalId) REFERENCES dbo.Personal (PersonalId);
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandTimeout = 180;
            cmd.CommandText = """
                IF OBJECT_ID(N'dbo.TaskStatusHistory', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.TaskStatusHistory (
                        HistoryId       INT NOT NULL IDENTITY(1, 1) PRIMARY KEY,
                        TaskId          INT NOT NULL,
                        FromStatus      NVARCHAR(50) NULL,
                        ToStatus        NVARCHAR(50) NOT NULL,
                        ChangedByUserId INT NULL,
                        ChangedAt       DATETIME2 NOT NULL CONSTRAINT DF_TaskStatusHistory_ChangedAt DEFAULT (SYSUTCDATETIME()),
                        CONSTRAINT FK_TaskStatusHistory_Task FOREIGN KEY (TaskId) REFERENCES dbo.Tasks (TaskId) ON DELETE CASCADE,
                        CONSTRAINT FK_TaskStatusHistory_User FOREIGN KEY (ChangedByUserId) REFERENCES dbo.Users (UserId)
                    );
                    CREATE INDEX IX_TaskStatusHistory_TaskId ON dbo.TaskStatusHistory (TaskId);
                END
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandTimeout = 180;
            cmd.CommandText = """
                IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.AuditLog (
                        AuditLogId      BIGINT NOT NULL IDENTITY(1, 1) PRIMARY KEY,
                        EntityType      NVARCHAR(100) NOT NULL,
                        EntityId        INT NOT NULL,
                        Action          NVARCHAR(50) NOT NULL,
                        UserId          INT NULL,
                        OccurredAt      DATETIME2 NOT NULL CONSTRAINT DF_AuditLog_OccurredAt DEFAULT (SYSUTCDATETIME()),
                        DiffJson        NVARCHAR(MAX) NULL,
                        CONSTRAINT FK_AuditLog_User FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId)
                    );
                    CREATE INDEX IX_AuditLog_Entity ON dbo.AuditLog (EntityType, EntityId);
                    CREATE INDEX IX_AuditLog_OccurredAt ON dbo.AuditLog (OccurredAt);
                END
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
