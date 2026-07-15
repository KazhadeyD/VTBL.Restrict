# Smoke checklist — live evidence (задача 4.2)

**OVERRIDE:** открытие Error Processing по `caseId` (без token/TTL gate). Пункты «invalid token» из черновика плана = **unknown / empty caseId → отказ UC-04**.

| ID | Шаг | Pass criteria | Evidence |
| --- | --- | --- | --- |
| TC-LIVE-01 | `dotnet run` → GET `/Upload` | HTTP 200; option MVK/TERRORISTS из store | |
| TC-LIVE-02 | Happy upload | файл as-is в `RemoteRoot`; batch в БД; **Published только если live RMQ**, иначе явный blocker | |
| TC-LIVE-03 | RMQ fail | `RabbitMqUploadNotifier` + недоступный Host → `Failed`; Retry без повторной записи файла | |
| TC-LIVE-04 | EP open | known `caseId` → форма из БД; unknown → отказ без stack; token ignored | |
| TC-LIVE-05 | EP resolve | required UserValue → `ResolvedByUser` | |
| TC-LIVE-06 | UC-05 ListType | INSERT active list type → появляется в select без смены кода UI | |

## Seed Error Processing (SQL)

```sql
DECLARE @CaseId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';
DECLARE @ItemId UNIQUEIDENTIFIER = '11111111-2222-3333-4444-555555555555';
DECLARE @ListTypeId INT = (SELECT ListTypeId FROM [restrict].ListType WHERE Code = N'MVK');

DELETE FROM [restrict].ErrorProcessingItem WHERE ErrorProcessingCaseId = @CaseId;
DELETE FROM [restrict].ErrorProcessingCase WHERE ErrorProcessingCaseId = @CaseId;

INSERT INTO [restrict].ErrorProcessingCase
    (ErrorProcessingCaseId, ListTypeId, AccessTokenHash, Status, ExpiresAt, SourceFilePath, CreatedBy)
VALUES
    (@CaseId, @ListTypeId, CONVERT(VARBINARY(32), HASHBYTES('SHA2_256', N'smoke')), N'Pending',
     DATEADD(day, 7, SYSUTCDATETIME()), N'\\smoke\source.xlsx', N'smoke');

INSERT INTO [restrict].ErrorProcessingItem
    (ErrorProcessingItemId, ErrorProcessingCaseId, FieldCode, RawValue, ParserMessage, IsRequired, SortOrder)
VALUES
    (@ItemId, @CaseId, N'SMOKE_FIELD', N'raw', N'parser msg', 1, 1);
```

Открыть: `/error-processing/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee`

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
