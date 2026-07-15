-- Raw mirror of Excel sheet RC (CBR-style list export).
-- Source sample: RC_F10_06_2026_T10_06_2026.xlsx (headers as-is, data not loaded here).
-- Date: 2026-07-15
-- Column mapping Excel header -> SQL (Latin for tooling; headers preserved in comments).

USE VTBL_Restrict;
GO

IF OBJECT_ID(N'[restrict].[RcListEntry]', N'U') IS NULL
BEGIN
    CREATE TABLE [restrict].[RcListEntry]
    (
        RcListEntryId UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_RcListEntry PRIMARY KEY
            CONSTRAINT DF_RcListEntry_Id DEFAULT (NEWSEQUENTIALID()),

        -- Excel: "Дата"
        [Дата] DATETIME2(3) NOT NULL,

        -- Excel: "Название" (observed up to ~3.6k chars)
        [Название] NVARCHAR(MAX) NOT NULL,

        -- Excel: "ИНН" (up to 10; often empty)
        [ИНН] NVARCHAR(32) NULL,

        -- Excel: "Адрес"
        [Адрес] NVARCHAR(MAX) NULL,

        -- Excel: "Сайт" (can be very long / multi-value text)
        [Сайт] NVARCHAR(MAX) NULL,

        -- Excel: "Признаки, установленные Банком России"
        [Признаки, установленные Банком России] NVARCHAR(512) NOT NULL,

        -- Excel: "Регионы"
        [Регионы] NVARCHAR(512) NULL,

        -- Excel: "Деятельность прекращена согласно ЕГРЮЛ" (0/1 int in file)
        [Деятельность прекращена согласно ЕГРЮЛ] BIT NOT NULL
            CONSTRAINT DF_RcListEntry_EgrulStopped DEFAULT (0),

        -- Excel: "Дополнительно"
        [Дополнительно] NVARCHAR(256) NULL,

        -- Excel: "Внутренний идентификатор записи" (source key)
        [Внутренний идентификатор записи] INT NOT NULL,

        -- Excel: "Дата последнего обновления данных"
        [Дата последнего обновления данных] DATETIME2(3) NOT NULL,

        -- Excel: "Тип записи"
        [Тип записи] NVARCHAR(64) NOT NULL,

        LoadedAtUtc DATETIME2(3) NOT NULL
            CONSTRAINT DF_RcListEntry_LoadedAtUtc DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT UX_RcListEntry_ExternalId UNIQUE ([Внутренний идентификатор записи])
    );

    CREATE INDEX IX_RcListEntry_RecordDate
        ON [restrict].[RcListEntry] ([Дата]);

    CREATE INDEX IX_RcListEntry_RecordType
        ON [restrict].[RcListEntry] ([Тип записи]);
END
GO
