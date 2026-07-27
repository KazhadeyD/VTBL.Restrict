-- VTBL_Restrict initial DDL
-- Date: 2026-07-15 (bracketed [restrict] — RESTRICT is reserved in T-SQL)
-- Schema: restrict
-- Review before applying to shared environments.

IF DB_ID(N'VTBL_Restrict') IS NULL
BEGIN
    CREATE DATABASE VTBL_Restrict;
END
GO

USE VTBL_Restrict;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'restrict')
    EXEC(N'CREATE SCHEMA [restrict]');
GO

IF OBJECT_ID(N'[restrict].[ListType]', N'U') IS NULL
BEGIN
    CREATE TABLE [restrict].ListType
    (
        ListTypeId        INT            NOT NULL IDENTITY(1,1) CONSTRAINT PK_ListType PRIMARY KEY,
        Code              NVARCHAR(64)   NOT NULL,
        Name              NVARCHAR(256)  NOT NULL,
        FolderSegment     NVARCHAR(128)  NOT NULL,
        RoutingKeySuffix  NVARCHAR(128)  NOT NULL,
        IsActive          BIT            NOT NULL CONSTRAINT DF_ListType_IsActive DEFAULT (1),
        CreatedAt         DATETIME2(3)   NOT NULL CONSTRAINT DF_ListType_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UX_ListType_Code UNIQUE (Code)
    );
END
GO

IF OBJECT_ID(N'[restrict].[UploadBatch]', N'U') IS NULL
BEGIN
    CREATE TABLE [restrict].UploadBatch
    (
        UploadBatchId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_UploadBatch PRIMARY KEY
                          CONSTRAINT DF_UploadBatch_Id DEFAULT (NEWSEQUENTIALID()),
        CorrelationId     UNIQUEIDENTIFIER NOT NULL,
        ListTypeId        INT              NOT NULL,
        OriginalFileName  NVARCHAR(512)    NOT NULL,
        StoredFilePath    NVARCHAR(1024)   NOT NULL,
        UploadedBy        NVARCHAR(256)    NULL,
        UploadedAt        DATETIME2(3)     NOT NULL CONSTRAINT DF_UploadBatch_UploadedAt DEFAULT (SYSUTCDATETIME()),
        NotifyStatus      NVARCHAR(32)     NOT NULL,
        CONSTRAINT UX_UploadBatch_CorrelationId UNIQUE (CorrelationId),
        CONSTRAINT FK_UploadBatch_ListType FOREIGN KEY (ListTypeId) REFERENCES [restrict].ListType (ListTypeId),
        CONSTRAINT CK_UploadBatch_NotifyStatus CHECK (NotifyStatus IN (N'Pending', N'Published', N'Failed'))
    );

    CREATE INDEX IX_UploadBatch_UploadedAt ON [restrict].UploadBatch (UploadedAt);
END
GO

IF OBJECT_ID(N'[restrict].[ErrorProcessingCase]', N'U') IS NULL
BEGIN
    CREATE TABLE [restrict].ErrorProcessingCase
    (
        ErrorProcessingCaseId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ErrorProcessingCase PRIMARY KEY
                            CONSTRAINT DF_ErrorProcessingCase_Id DEFAULT (NEWSEQUENTIALID()),
        ListTypeId          INT              NOT NULL,
        UploadCorrelationId UNIQUEIDENTIFIER NULL,
        AccessTokenHash     VARBINARY(32)    NOT NULL,
        Status              NVARCHAR(32)     NOT NULL,
        ExpiresAt           DATETIME2(3)     NOT NULL,
        SourceFilePath      NVARCHAR(1024)   NULL,
        CreatedAt           DATETIME2(3)     NOT NULL CONSTRAINT DF_ErrorProcessingCase_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy           NVARCHAR(256)    NULL,
        ResolvedAt          DATETIME2(3)     NULL,
        ResolvedBy          NVARCHAR(256)    NULL,
        CONSTRAINT FK_ErrorProcessingCase_ListType FOREIGN KEY (ListTypeId) REFERENCES [restrict].ListType (ListTypeId),
        CONSTRAINT CK_ErrorProcessingCase_Status CHECK (Status IN (N'Pending', N'ResolvedByUser', N'Expired', N'Cancelled'))
    );

    CREATE INDEX IX_ErrorProcessingCase_Status_ExpiresAt ON [restrict].ErrorProcessingCase (Status, ExpiresAt);
    CREATE INDEX IX_ErrorProcessingCase_Status_CreatedAt ON [restrict].ErrorProcessingCase (Status, CreatedAt DESC);
END
GO

-- Idempotent: applies on existing DBs where CREATE TABLE gate already fired (see also docs/db/migrations/).
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ErrorProcessingCase_Status_CreatedAt'
      AND object_id = OBJECT_ID(N'[restrict].[ErrorProcessingCase]')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_ErrorProcessingCase_Status_CreatedAt
        ON [restrict].[ErrorProcessingCase] ([Status], [CreatedAt] DESC);
END
GO

IF OBJECT_ID(N'[restrict].[ErrorProcessingItem]', N'U') IS NULL
BEGIN
    CREATE TABLE [restrict].ErrorProcessingItem
    (
        ErrorProcessingItemId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ErrorProcessingItem PRIMARY KEY
                          CONSTRAINT DF_ErrorProcessingItem_Id DEFAULT (NEWSEQUENTIALID()),
        ErrorProcessingCaseId     UNIQUEIDENTIFIER NOT NULL,
        FieldCode         NVARCHAR(128)    NOT NULL,
        RowNumber         INT              NULL,
        RawValue          NVARCHAR(MAX)    NULL,
        ParserMessage     NVARCHAR(1024)   NULL,
        UserValue         NVARCHAR(MAX)    NULL,
        IsRequired        BIT              NOT NULL CONSTRAINT DF_ErrorProcessingItem_IsRequired DEFAULT (1),
        SortOrder         INT              NOT NULL CONSTRAINT DF_ErrorProcessingItem_SortOrder DEFAULT (0),
        CONSTRAINT FK_ErrorProcessingItem_ErrorProcessingCase FOREIGN KEY (ErrorProcessingCaseId)
            REFERENCES [restrict].ErrorProcessingCase (ErrorProcessingCaseId) ON DELETE CASCADE
    );

    CREATE INDEX IX_ErrorProcessingItem_ErrorProcessingCaseId
        ON [restrict].ErrorProcessingItem (ErrorProcessingCaseId, SortOrder);
END
GO

-- Seed MVP list types
MERGE [restrict].ListType AS t
USING (VALUES
    (N'MVK', N'МВК', N'mvk', N'mvk', 1),
    (N'TERRORISTS', N'Террористы', N'terrorists', N'terrorists', 1),
    (N'NFA', N'Нелегальная финансовая деятельность', N'nfa', N'nfa', 1)
) AS s (Code, Name, FolderSegment, RoutingKeySuffix, IsActive)
ON t.Code = s.Code
WHEN NOT MATCHED THEN
    INSERT (Code, Name, FolderSegment, RoutingKeySuffix, IsActive)
    VALUES (s.Code, s.Name, s.FolderSegment, s.RoutingKeySuffix, s.IsActive);
GO
