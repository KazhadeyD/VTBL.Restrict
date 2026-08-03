# VTBL.Restrict.Loader

UI для работы с **рестриктивными списками** (МВК, Террористы и др.): загрузка Excel/CSV на удалённую папку **as is** (без разбора содержимого) и уведомление сервиса парсинга через RabbitMQ (включая retry).

Парсинг и интерпретация содержимого файлов в этом сервисе **не выполняются**.

## Структура

| Элемент | Описание |
| --- | --- |
| `VTBL.Restrict.Loader.sln` | Solution Visual Studio (целевой TFM: **net5.0**) |
| `VTBL.Restrict.Loader.UI` | Веб-приложение (Razor Pages, `.NET 5.0`) |
| `VTBL.Restrict.Loader.Domain` | Доменные enums/value helpers |
| `VTBL.Restrict.Loader.Application` | Сценарии upload / retry notify, порты (interfaces), observability upload/publish/retry |
| `VTBL.Restrict.Loader.Context` | EF Core: `RestrictDbContext`, ListType/UploadBatch/RcListEntry |
| `VTBL.Restrict.Loader.Infrastructure` | Файловая шара, RabbitMQ, InMemory ListType/Batch; подключает Context при RestrictDb |
| `tests/VTBL.Restrict.Loader.Tests` | Канонический E2E-скелет (WebApplicationFactory, stub expectations) |
| `tests/VTBL.Restrict.Loader.Application.Tests` | Модульные тесты Application stubs |
| `tests/VTBL.Restrict.Loader.UI.Tests` | Unit-тесты PageModels |
| `docker-compose.yml` | MSSQL Server 2022 (отдельный стек, порт **1434**) |
| `docs/` | Проектная документация (ТЗ, БД, интеграции, экраны, архитектура) |

## Документация

См. [docs/README.md](docs/README.md). Локальный стенд: [docs/runbook-local.md](docs/runbook-local.md), smoke: [docs/smoke-checklist.md](docs/smoke-checklist.md).

## Observability

Ключевые операции **upload / publish / retry** пишут structured logs (`ILogger`) с корреляцией: `correlationId`, `listType`, код ошибки.

## Запуск (Docker MSSQL + UI)

```bash
copy .env.example .env
docker compose up -d

dotnet run --project VTBL.Restrict.Loader.UI --urls http://localhost:5000
```

Development connection string: `localhost,1434` / БД `VTBL_Restrict` / sa (пароль из `.env` / `.env.example`). Пустой `RabbitMq:Host` → InMemory notifier (**не** live RMQ).

## Сборка

```bash
dotnet build VTBL.Restrict.Loader.sln
dotnet test VTBL.Restrict.Loader.sln
```

## История изменений

### 03.08.2026
- Финальная верификация эпика UC-RM (задача 5.1): `dotnet build`/`dotnet test` green (51), grep EP/observability-маркеров = 0, DDL/docker без ErrorProcessing*, Docker `down -v`+init EpTableCount=0, UI smoke Upload 200 / бывшие EP-URL 404; commit не создавался исполнителем
- Удалён **Error Processing** из Loader (UI / Application / Domain / Context / Infrastructure / тесты / DDL / docs); продукт — только upload → FileShare + RMQ + retry; UC-03/UC-04 superseded; SpecialCase не возвращается (эпик UC-RM, docs supersession — задача 4.1)
- UC-RM-05 (этап 3.1, Вариант A): вычищены ErrorProcessing* из `docs/db/05-ddl.sql` / `docker/mssql/init/02-schema.sql`; удалены `04-ep-index-*` и migration `*ErrorProcessing*`; обновлены docs/db domain/ER/physical/access, init README, runbook (`docker compose down -v` + чистый init — единственный канон очистки volume); DROP-миграция не создавалась; сохранены ListType (+ seed), UploadBatch, RcListEntry
- UC-RM-02 (этап 2.3): удалены Application ErrorProcessing / `IErrorProcessingStore`, Domain `ErrorProcessingStatus` / `AccessTokenHasher`, EP-only observability (`BeginOpen`/`BeginSave`/`BeginList`, `SensitiveLog`, `KeyCaseId`/`KeyTokenPresent`); сохранены BeginUpload/Publish/Retry
- UC-RM-03 (этап 2.2): удалены EF/InMemory EP stores, EP DbSet/Fluent и DI-регистрации EP (store/commands/queries); порт `IErrorProcessingStore` и Application EP остаются до 2.3
- UC-RM-01 (этап 2.1): удалены Razor Pages `/error-processing*`, `ErrorProcessingFormModel` и пункт navbar «Обработка ошибок»; Upload E2E проверяет отсутствие EP в HTML и non-success на бывших маршрутах
- UC-RM-04 (этап 1.1): удалены EP-тесты и EP-fake harness из `tests/`; вычищены смешанные `RestrictWebAppFactory`, `CorrelationLoggingTests`, `ApplicationStubCommandsTests` — тестовый контур только Upload/Retry/ListType/observability upload
- Полный rename: папка / solution / проекты / namespaces `VTBL.Restrict` → `VTBL.Restrict.Loader` (типы вроде `RestrictDbContext`, БД `VTBL_Restrict`, Docker-сервисы без изменений имени)

### 27.07.2026
- Upload UI: кнопка «Удалить файл» — сброс выбранного файла (DnD / picker) до отправки формы; `upload-dnd.js` + `data-upload-file-clear`
- UC-05: добавлен активный тип списка `NFA` («Нелегальная финансовая деятельность») в seed справочника `ListType` (SQL: `docs/db/05-ddl.sql`, docker init: `docker/mssql/init/02-schema.sql`) и in-memory store (`InMemoryListTypeReadStore`) для dev/test.
- Обновлены тест и каталог типов: `InMemoryListTypeReadStoreTests` проверяет `NFA`; `docs/list-types.md` синхронизирован с новым кодом/маршрутизацией `nfa`.

### 15.07.2026
- **EP-4.1:** docs supersession (`ExpiresAt` не gate UI до security-эпика) — `01-domain.md`, `architecture.md`, `screens-flow.md`, `runbook-local.md`; smoke TC-LIVE-EP-01…03 в `smoke-checklist.md`
- **EP-3.1…2.5:** usable Error Processing UI — список `/error-processing` + кейс с resolve; Application/E2E регрессия (Db-fail, deep-link, empty items)
- EP-2.1: идемпотентный index-patch `IX_ErrorProcessingCase_Status_CreatedAt` (`Status`, `CreatedAt DESC`) — `docs/db/migrations/` + docker `04-ep-index-status-createdat.sql`; канон `05-ddl.sql` / `02-schema.sql` / `03-physical.md`
- EP-1.3: E2E/unit скелет списка Error Processing — stub-ожидания empty (`ErrorProcessingListE2ETests`, `ListPendingErrorProcessingCasesQueryTests` + InMemory `ListPendingSummariesAsync`); регресс Upload/Get form
- EP-1.2: Razor List `/error-processing` (stub empty-state) + navbar «Обработка ошибок»; FormModel `RowNumber` / `UploadCorrelationId`; Index каркас (метка ExpiresAt, «К списку», ReasonDb UX)
- EP-1.1: контракты списка Error Processing — `ListPendingErrorProcessingCasesQuery` (stub: пустой Items), `ErrorProcessingCaseSummaryRecord` / DTO, enrich Get (`CreatedAtUtc` / `UploadCorrelationId`, `ReasonDb`/`DbError`); EF List — stub empty, InMemory — Pending filter
- Доступ к БД: Dapper заменён на **EF Core 5**; проект `VTBL.Restrict.Loader.Context` (`RestrictDbContext` + Ef*Store); схема по-прежнему из SQL init
- Таблица `[restrict].[RcListEntry]` — колонки «как есть» из Excel листа `RC` (файл не загружался); DDL `docs/db/06-rc-list-entry.sql`
- Docker Compose: отдельный MSSQL (`vtbl-restrict-mssql`, порт **1434**), init DDL `VTBL_Restrict`; Development подключён к контейнеру
- Задача 4.2: DDL `[restrict]` (reserved), runbook/smoke checklist, Development LocalDB + RemoteRoot; live smoke + blockers (RMQ broker)
- Задача 4.1: корреляционное логирование upload/publish/retry/open/save (`OperationLogScope`, `ILogger`); без raw token / UserValue в логах
- Задача 3.2: `ResolveErrorProcessingCommand` — валидация обязательных UserValue, атомарный resolve Pending→ResolvedByUser (Sql + InMemory), UI placeholder `/error-processing/{caseId}` (edit/success/validation/conflict/not found); без token/TTL gate
- Задача 3.1: `GetErrorProcessingForOperatorQuery` + `SqlErrorProcessingCaseStore` / `InMemoryErrorProcessingCaseStore` — открытие `/error-processing/{caseId}` только из БД (EC-05), без token/TTL/auth gate; ветки Pending / NotFound / ResolvedByUser (read-only) / Expired|Cancelled
- Терминология: «Особый случай» / SpecialCase → **«Обработка ошибок»** / `ErrorProcessing` (маршрут `/error-processing/{caseId}`, таблицы `ErrorProcessingCase` / `ErrorProcessingItem`)
- Security epic отложен: Error Processing по `caseId` без token/TTL/auth; ТЗ, architecture, задачи 3.1/3.2 обновлены
- Задача 2.5: Upload UI — DnD + «Выбрать файл», явная «Отправить», `_UploadResult`, mapper кодов ошибок UC-02, retry при Rmq fail; Error page без stack
- Задача 2.4: `RetryUploadNotificationCommand` — повтор RMQ без FileShare; ветки Failed/Pending/Published/not found
- Задача 2.3: полный upload flow EC-08 (WriteAsIs → UploadBatch Pending → RMQ → Published/Failed), `SqlUploadBatchStore`, `RabbitMqUploadNotifier`
- Задача 2.2: `UncFileShareStore` (as-is, temp+rename), `PathBuilder`, реальная запись после валидации; RMQ — в 2.3
- Задача 2.1: оболочка EC-02 (`UploadShellValidator` / `FileNameSanitizer`), `IListTypeReadStore` (SQL Dapper + InMemory seed MVK/TERRORISTS/NFA), Upload GET из store; без парсинга Excel/CSV
- Задача 1.3: канонический E2E-проект `tests/VTBL.Restrict.Loader.Tests` (WebApplicationFactory) + расширенные stub unit-тесты; E2E вынесены из UI.Tests
- Задача 1.2: Razor Pages `/Upload`, `/Upload/Retry`, `/error-processing/{caseId}` + DI stubs (`AddRestrictInfrastructure` в Startup)
- Задача 1.1: добавлены проекты `Domain` / `Application` / `Infrastructure` (net5.0) с портами и stub-командами; UI ссылается на Application+Infrastructure
- Целевой TFM solution зафиксирован: **net5.0** (миграция net8 отложена / запрещена product owner)
- Утверждены этапы пайплайна: Анализ, Архитектура, Планирование (артефакты в локальном `docs/implementation/`)
- Утверждённая архитектура из пайплайна скопирована в versioned [`docs/architecture.md`](docs/architecture.md)
- Инициализирован git-репозиторий; расширен `.gitignore` (.vs, bin/obj, `docs/implementation/`)
- Зафиксирован инвариант загрузки: файл передаётся **as is**, UI не парсит и не просматривает содержимое (только тип/расширение/размер)

### 14.07.2026
- Добавлен пакет проектирования в `docs/`: ТЗ, типы списков, БД (domain/ER/physical/access/DDL), интеграция папка+RabbitMQ, экраны, архитектура
- Solution переведён с формата `VTBL.Restrict.Loader.slnx` на классический `VTBL.Restrict.Loader.sln`
- Файл `VTBL.Restrict.Loader.slnx` удалён
- Добавлен `README.md`
