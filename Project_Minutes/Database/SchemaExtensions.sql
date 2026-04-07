-- Ejecutar en la base Minutes (o la que uses) una sola vez.
-- Firmas de minuta: un registro por (MinuteId, UserId). Si ya tienes duplicados, límpialos antes del índice único.

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
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Signatures_Minute_User' AND object_id = OBJECT_ID(N'dbo.Signatures'))
BEGIN
    CREATE UNIQUE INDEX UX_Signatures_Minute_User ON dbo.Signatures (MinuteId, UserId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Participants') AND name = N'Position')
    ALTER TABLE dbo.Participants ADD Position NVARCHAR(150) NULL;
GO
