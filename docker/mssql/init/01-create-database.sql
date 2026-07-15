-- Create VTBL_Restrict if missing (idempotent).
-- Full schema/seed: docs/db/05-ddl.sql (also creates DB if needed).

IF DB_ID(N'VTBL_Restrict') IS NULL
BEGIN
    CREATE DATABASE VTBL_Restrict;
END
GO
