-- VTBL_Restrict initial DDL
-- Date: 2026-07-14
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
    EXEC(N'CREATE SCHEMA restrict');
GO

CREATE TABLE restrict.ListType
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
GO

CREATE TABLE restrict.UploadBatch
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
    CONSTRAINT FK_UploadBatch_ListType FOREIGN KEY (ListTypeId) REFERENCES restrict.ListType (ListTypeId),
    CONSTRAINT CK_UploadBatch_NotifyStatus CHECK (NotifyStatus IN (N'Pending', N'Published', N'Failed'))
);
GO

CREATE INDEX IX_UploadBatch_UploadedAt ON restrict.UploadBatch (UploadedAt);
GO

CREATE TABLE restrict.SpecialCase
(
    SpecialCaseId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SpecialCase PRIMARY KEY
                        CONSTRAINT DF_SpecialCase_Id DEFAULT (NEWSEQUENTIALID()),
    ListTypeId          INT              NOT NULL,
    UploadCorrelationId UNIQUEIDENTIFIER NULL,
    AccessTokenHash     VARBINARY(32)    NOT NULL,
    Status              NVARCHAR(32)     NOT NULL,
    ExpiresAt           DATETIME2(3)     NOT NULL,
    SourceFilePath      NVARCHAR(1024)   NULL,
    CreatedAt           DATETIME2(3)     NOT NULL CONSTRAINT DF_SpecialCase_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy           NVARCHAR(256)    NULL,
    ResolvedAt          DATETIME2(3)     NULL,
    ResolvedBy          NVARCHAR(256)    NULL,
    CONSTRAINT FK_SpecialCase_ListType FOREIGN KEY (ListTypeId) REFERENCES restrict.ListType (ListTypeId),
    CONSTRAINT CK_SpecialCase_Status CHECK (Status IN (N'Pending', N'ResolvedByUser', N'Expired', N'Cancelled'))
);
GO

CREATE INDEX IX_SpecialCase_Status_ExpiresAt ON restrict.SpecialCase (Status, ExpiresAt);
GO

CREATE TABLE restrict.SpecialCaseItem
(
    SpecialCaseItemId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SpecialCaseItem PRIMARY KEY
                      CONSTRAINT DF_SpecialCaseItem_Id DEFAULT (NEWSEQUENTIALID()),
    SpecialCaseId     UNIQUEIDENTIFIER NOT NULL,
    FieldCode         NVARCHAR(128)    NOT NULL,
    RowNumber         INT              NULL,
    RawValue          NVARCHAR(MAX)    NULL,
    ParserMessage     NVARCHAR(1024)   NULL,
    UserValue         NVARCHAR(MAX)    NULL,
    IsRequired        BIT              NOT NULL CONSTRAINT DF_SpecialCaseItem_IsRequired DEFAULT (1),
    SortOrder         INT              NOT NULL CONSTRAINT DF_SpecialCaseItem_SortOrder DEFAULT (0),
    CONSTRAINT FK_SpecialCaseItem_SpecialCase FOREIGN KEY (SpecialCaseId)
        REFERENCES restrict.SpecialCase (SpecialCaseId) ON DELETE CASCADE
);
GO

CREATE INDEX IX_SpecialCaseItem_SpecialCaseId
    ON restrict.SpecialCaseItem (SpecialCaseId, SortOrder);
GO

-- Seed MVP list types
MERGE restrict.ListType AS t
USING (VALUES
    (N'MVK', N'МВК', N'mvk', N'mvk', 1),
    (N'TERRORISTS', N'Террористы', N'terrorists', N'terrorists', 1)
) AS s (Code, Name, FolderSegment, RoutingKeySuffix, IsActive)
ON t.Code = s.Code
WHEN NOT MATCHED THEN
    INSERT (Code, Name, FolderSegment, RoutingKeySuffix, IsActive)
    VALUES (s.Code, s.Name, s.FolderSegment, s.RoutingKeySuffix, s.IsActive);
GO
