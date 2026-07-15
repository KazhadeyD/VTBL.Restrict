# БД Restrict: физическая модель

**Дата:** 14.07.2026  
**СУБД:** Microsoft SQL Server  
**База:** `VTBL_Restrict`  
**Схема:** `restrict`

## 1. Соглашения

- PK: `INT IDENTITY` для справочников; `UNIQUEIDENTIFIER` для операционных сущностей.
- Время: `DATETIME2(3)` UTC.
- Строки: `NVARCHAR`; пути до `NVARCHAR(1024)`.
- Мягкое удаление типов не требуется на MVP (`IsActive`).

## 2. Таблицы

### restrict.ListType

| Column | Type | Null | Notes |
| --- | --- | --- | --- |
| ListTypeId | INT IDENTITY | N | PK |
| Code | NVARCHAR(64) | N | UK |
| Name | NVARCHAR(256) | N | |
| FolderSegment | NVARCHAR(128) | N | |
| RoutingKeySuffix | NVARCHAR(128) | N | |
| IsActive | BIT | N | DEFAULT 1 |
| CreatedAt | DATETIME2(3) | N | DEFAULT SYSUTCDATETIME() |

Индексы: `UX_ListType_Code` UNIQUE (`Code`).

### restrict.UploadBatch

| Column | Type | Null | Notes |
| --- | --- | --- | --- |
| UploadBatchId | UNIQUEIDENTIFIER | N | PK, DEFAULT NEWSEQUENTIALID() |
| CorrelationId | UNIQUEIDENTIFIER | N | UK |
| ListTypeId | INT | N | FK → ListType |
| OriginalFileName | NVARCHAR(512) | N | |
| StoredFilePath | NVARCHAR(1024) | N | |
| UploadedBy | NVARCHAR(256) | Y | |
| UploadedAt | DATETIME2(3) | N | |
| NotifyStatus | NVARCHAR(32) | N | Pending/Published/Failed |

Индексы: `UX_UploadBatch_CorrelationId`; `IX_UploadBatch_UploadedAt`.

### restrict.ErrorProcessingCase

| Column | Type | Null | Notes |
| --- | --- | --- | --- |
| ErrorProcessingCaseId | UNIQUEIDENTIFIER | N | PK |
| ListTypeId | INT | N | FK |
| UploadCorrelationId | UNIQUEIDENTIFIER | Y | логическая связь |
| AccessTokenHash | VARBINARY(32) | N | SHA-256 |
| Status | NVARCHAR(32) | N | |
| ExpiresAt | DATETIME2(3) | N | |
| SourceFilePath | NVARCHAR(1024) | Y | |
| CreatedAt | DATETIME2(3) | N | |
| CreatedBy | NVARCHAR(256) | Y | сервис парсера |
| ResolvedAt | DATETIME2(3) | Y | |
| ResolvedBy | NVARCHAR(256) | Y | |

Индексы:
- `IX_ErrorProcessingCase_Status_ExpiresAt` (`Status`, `ExpiresAt`);
- доступ по Id — PK.

CHECK (рекомендуется): `Status IN ('Pending','ResolvedByUser','Expired','Cancelled')`.

### restrict.ErrorProcessingItem

| Column | Type | Null | Notes |
| --- | --- | --- | --- |
| ErrorProcessingItemId | UNIQUEIDENTIFIER | N | PK |
| ErrorProcessingCaseId | UNIQUEIDENTIFIER | N | FK ON DELETE CASCADE |
| FieldCode | NVARCHAR(128) | N | |
| RowNumber | INT | Y | |
| RawValue | NVARCHAR(MAX) | Y | |
| ParserMessage | NVARCHAR(1024) | Y | |
| UserValue | NVARCHAR(MAX) | Y | |
| IsRequired | BIT | N | DEFAULT 1 |
| SortOrder | INT | N | DEFAULT 0 |

Индексы: `IX_ErrorProcessingItem_ErrorProcessingCaseId` (`ErrorProcessingCaseId`, `SortOrder`).

## 3. Seed ListType (MVP)

| Code | Name | FolderSegment | RoutingKeySuffix |
| --- | --- | --- | --- |
| MVK | МВК | mvk | mvk |
| TERRORISTS | Террористы | terrorists | terrorists |

## 4. Подключения

Отдельные SQL login / Windows УЗ:

- `restrict_ui` — права см. [04-access.md](04-access.md)
- `restrict_parser` — права см. [04-access.md](04-access.md)

Connection string UI — в конфигурации приложения, не в репозитории с секретами.
