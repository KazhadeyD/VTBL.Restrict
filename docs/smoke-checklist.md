# Smoke checklist — live evidence

**OVERRIDE:** Error Processing по `caseId` (без token/TTL gate). Список: `GET /error-processing` (только Pending).  
Пункты «invalid token» = **unknown / empty caseId → отказ UC-04**.

| ID | Шаг | Pass criteria | Evidence |
| --- | --- | --- | --- |
| TC-LIVE-01 | `dotnet run` → GET `/Upload` | HTTP 200; option MVK/TERRORISTS/NFA из store | |
| TC-LIVE-02 | Happy upload | файл as-is в `RemoteRoot`; batch в БД; **Published только если live RMQ**, иначе явный blocker | |
| TC-LIVE-03 | RMQ fail | `RabbitMqUploadNotifier` + недоступный Host → `Failed`; Retry без повторной записи файла | |
| TC-LIVE-04 | EP open | known `caseId` → форма из БД; unknown → отказ без stack; token ignored | |
| TC-LIVE-05 | EP resolve | required UserValue → `ResolvedByUser` | |
| TC-LIVE-06 | UC-05 ListType | INSERT active list type → появляется в select без смены кода UI | |
| TC-LIVE-EP-01 | GET `/error-processing` + Pending seed | таблица Pending; новые сверху (`CreatedAt DESC`); ссылка открывает кейс | |
| TC-LIVE-EP-02 | GET `/error-processing` без Pending | empty-state («Нет кейсов…»), без stack | |
| TC-LIVE-EP-03 | Index on target DB | существует `IX_ErrorProcessingCase_Status_CreatedAt` (`Status`, `CreatedAt DESC`) | |

## Seed Error Processing (SQL)

Для **TC-LIVE-EP-01** — два Pending с разным `CreatedAt` (новые сверху):

```sql
DECLARE @CaseNew UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';
DECLARE @CaseOld UNIQUEIDENTIFIER = 'bbbbbbbb-cccc-dddd-eeee-ffffffffffff';
DECLARE @ItemNew UNIQUEIDENTIFIER = '11111111-2222-3333-4444-555555555555';
DECLARE @ListTypeId INT = (SELECT ListTypeId FROM [restrict].ListType WHERE Code = N'MVK');

DELETE FROM [restrict].ErrorProcessingItem WHERE ErrorProcessingCaseId IN (@CaseNew, @CaseOld);
DELETE FROM [restrict].ErrorProcessingCase WHERE ErrorProcessingCaseId IN (@CaseNew, @CaseOld);

INSERT INTO [restrict].ErrorProcessingCase
    (ErrorProcessingCaseId, ListTypeId, AccessTokenHash, Status, ExpiresAt, SourceFilePath, CreatedAt, CreatedBy)
VALUES
    (@CaseOld, @ListTypeId, CONVERT(VARBINARY(32), HASHBYTES('SHA2_256', N'smoke-old')), N'Pending',
     DATEADD(day, 7, SYSUTCDATETIME()), N'\\smoke\old.xlsx', DATEADD(day, -2, SYSUTCDATETIME()), N'smoke'),
    (@CaseNew, @ListTypeId, CONVERT(VARBINARY(32), HASHBYTES('SHA2_256', N'smoke-new')), N'Pending',
     DATEADD(day, 7, SYSUTCDATETIME()), N'\\smoke\new.xlsx', SYSUTCDATETIME(), N'smoke');

INSERT INTO [restrict].ErrorProcessingItem
    (ErrorProcessingItemId, ErrorProcessingCaseId, FieldCode, RawValue, ParserMessage, IsRequired, SortOrder)
VALUES
    (@ItemNew, @CaseNew, N'SMOKE_FIELD', N'raw', N'parser msg', 1, 1);
```

- Список: `/error-processing` — `@CaseNew` выше `@CaseOld`
- Кейс: `/error-processing/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee`

Для **TC-LIVE-EP-02**: удалить Pending seed или временно использовать пустую БД без Pending.

Для **TC-LIVE-EP-03**:

```sql
SELECT i.name, c.name AS col, ic.is_descending_key
FROM sys.indexes i
JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.object_id = OBJECT_ID(N'[restrict].[ErrorProcessingCase]')
  AND i.name = N'IX_ErrorProcessingCase_Status_CreatedAt'
ORDER BY ic.key_ordinal;
```

## UC-05 seed

```sql
MERGE [restrict].ListType AS t
USING (VALUES (N'SMOKE_TYPE', N'Smoke Type', N'smoke', N'smoke', 1)) AS s (Code, Name, FolderSegment, RoutingKeySuffix, IsActive)
ON t.Code = s.Code
WHEN NOT MATCHED THEN INSERT (Code, Name, FolderSegment, RoutingKeySuffix, IsActive)
VALUES (s.Code, s.Name, s.FolderSegment, s.RoutingKeySuffix, s.IsActive)
WHEN MATCHED THEN UPDATE SET IsActive = 1, Name = s.Name;
```

## RMQ fail (TC-LIVE-03)

Временно в Development:

```json
"RabbitMq": { "Host": "127.0.0.1", "Username": "guest", "Password": "guest" }
```

(порт 5672 закрыт → publish fail). После проверки вернуть `"Host": ""` если брокера нет.
