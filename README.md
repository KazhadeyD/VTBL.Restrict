# VTBL.Restrict

UI для работы с **рестриктивными списками** (МВК, Террористы и др.): загрузка Excel/CSV на удалённую папку **as is** (без разбора содержимого), уведомление сервиса парсинга через RabbitMQ, **обработка ошибок** по ссылке из письма с данными из БД.

Парсинг и интерпретация содержимого файлов в этом сервисе **не выполняются**.

## Структура

| Элемент | Описание |
| --- | --- |
| `VTBL.Restrict.sln` | Solution Visual Studio (целевой TFM: **net5.0**) |
| `VTBL.Restrict.UI` | Веб-приложение (Razor Pages, `.NET 5.0`) |
| `VTBL.Restrict.Domain` | Доменные enums/value helpers |
| `VTBL.Restrict.Application` | Сценарии upload/error-processing, порты (interfaces) |
| `VTBL.Restrict.Context` | EF Core: `RestrictDbContext`, сущности, EF-stores |
| `VTBL.Restrict.Infrastructure` | Файловая шара, RabbitMQ, InMemory-заглушки; подключает Context при RestrictDb |
| `tests/VTBL.Restrict.Tests` | Канонический E2E-скелет (WebApplicationFactory, stub expectations) |
| `tests/VTBL.Restrict.Application.Tests` | Модульные тесты Application stubs |
| `tests/VTBL.Restrict.UI.Tests` | Unit-тесты PageModels |
| `docker-compose.yml` | MSSQL Server 2022 (отдельный стек, порт **1434**) |
| `docs/` | Проектная документация (ТЗ, БД, интеграции, экраны, архитектура) |

## Документация

См. [docs/README.md](docs/README.md). Локальный стенд: [docs/runbook-local.md](docs/runbook-local.md), smoke: [docs/smoke-checklist.md](docs/smoke-checklist.md).

## Observability

Ключевые операции **upload / publish / retry / open / save** пишут structured logs (`ILogger`) с корреляцией: `correlationId`, `caseId`, `listType`, код ошибки. Сырой query `token` и значения `UserValue` в логи не попадают (только `tokenPresent=present|absent`). Маскирование PII шире — security epic (deferred).

## Запуск (Docker MSSQL + UI)

```bash
copy .env.example .env
docker compose up -d

dotnet run --project VTBL.Restrict.UI --urls http://localhost:5000
```

Development connection string: `localhost,1434` / БД `VTBL_Restrict` / sa (пароль из `.env` / `.env.example`). Пустой `RabbitMq:Host` → InMemory notifier (**не** live RMQ).

## Сборка

```bash
dotnet build VTBL.Restrict.sln
dotnet test VTBL.Restrict.sln
```

## История изменений

### 15.07.2026
- Доступ к БД: Dapper заменён на **EF Core 5**; проект `VTBL.Restrict.Context` (`RestrictDbContext` + Ef*Store); схема по-прежнему из SQL init
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
- Задача 2.1: оболочка EC-02 (`UploadShellValidator` / `FileNameSanitizer`), `IListTypeReadStore` (SQL Dapper + InMemory seed MVK/TERRORISTS), Upload GET из store; без парсинга Excel/CSV
- Задача 1.3: канонический E2E-проект `tests/VTBL.Restrict.Tests` (WebApplicationFactory) + расширенные stub unit-тесты; E2E вынесены из UI.Tests
- Задача 1.2: Razor Pages `/Upload`, `/Upload/Retry`, `/error-processing/{caseId}` + DI stubs (`AddRestrictInfrastructure` в Startup)
- Задача 1.1: добавлены проекты `Domain` / `Application` / `Infrastructure` (net5.0) с портами и stub-командами; UI ссылается на Application+Infrastructure
- Целевой TFM solution зафиксирован: **net5.0** (миграция net8 отложена / запрещена product owner)
- Утверждены этапы пайплайна: Анализ, Архитектура, Планирование (артефакты в локальном `docs/implementation/`)
- Утверждённая архитектура из пайплайна скопирована в versioned [`docs/architecture.md`](docs/architecture.md)
- Инициализирован git-репозиторий; расширен `.gitignore` (.vs, bin/obj, `docs/implementation/`)
- Зафиксирован инвариант загрузки: файл передаётся **as is**, UI не парсит и не просматривает содержимое (только тип/расширение/размер)

### 14.07.2026
- Добавлен пакет проектирования в `docs/`: ТЗ, типы списков, БД (domain/ER/physical/access/DDL), интеграция папка+RabbitMQ, экраны, архитектура
- Solution переведён с формата `VTBL.Restrict.slnx` на классический `VTBL.Restrict.sln`
- Файл `VTBL.Restrict.slnx` удалён
- Добавлен `README.md`
