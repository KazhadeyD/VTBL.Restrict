-- Idempotent index patch for ErrorProcessingCase (Status, CreatedAt DESC).
-- Must run outside IF OBJECT_ID(...ErrorProcessingCase...) IS NULL so existing DBs get the index.
-- Date: 2026-07-15

USE VTBL_Restrict;
GO

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
