-- Mirror of docs/db/migrations/20260715_IX_ErrorProcessingCase_Status_CreatedAt.sql
-- Idempotent; runs always (not gated by CREATE TABLE). Safe on fresh and existing volumes.
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
