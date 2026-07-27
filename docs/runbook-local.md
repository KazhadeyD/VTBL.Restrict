# Runbook: локальный стенд VTBL.Restrict.UI (net5.0)

**Дата:** 15.07.2026

## Предпосылки

| Компонент | MVP / smoke |
| --- | --- |
| .NET SDK с поддержкой `net5.0` | обязательно |
| Docker Desktop | MSSQL через `docker-compose` |
| Каталог `RestrictStorage:RemoteRoot` | локальный путь или UNC |
| RabbitMQ | для live publish; пустой `RabbitMq:Host` → InMemory notifier (не live broker) |

## 1. MSSQL в Docker (отдельный стек)

Контейнер **`vtbl-restrict-mssql`**, порт хоста **`1434→1433`** (не пересекается с ExportMessage на 1433).

```bash
# из корня репозитория
copy .env.example .env   # Windows: Copy-Item .env.example .env
docker compose up -d
```

`mssql-init` после healthcheck применит schema SQL. Приложение читает БД через **EF Core** (`VTBL.Restrict.Context`), не Dapper.

1. `docker/mssql/init/01-create-database.sql`
2. `docker/mssql/init/02-schema.sql` (копия `docs/db/05-ddl.sql` — при смене DDL синхронизируй оба)
3. `docker/mssql/init/03-rc-list-entry.sql`
4. `docker/mssql/init/04-ep-index-status-createdat.sql` — идемпотентный index-patch (`IX_ErrorProcessingCase_Status_CreatedAt`); канон: `docs/db/migrations/20260715_IX_ErrorProcessingCase_Status_CreatedAt.sql`

На уже существующем volume (без recreate) примени migration с хоста или перезапусти `mssql-init`:

```bash
sqlcmd -S localhost,1434 -U sa -P "ChangeMe_Str0ng!" -C -i docs/db/migrations/20260715_IX_ErrorProcessingCase_Status_CreatedAt.sql
```

Проверка с хоста:

```bash
sqlcmd -S localhost,1434 -U sa -P "ChangeMe_Str0ng!" -C -d VTBL_Restrict -Q "SELECT Code FROM [restrict].ListType;"
sqlcmd -S localhost,1434 -U sa -P "ChangeMe_Str0ng!" -C -d VTBL_Restrict -Q "SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID(N'[restrict].[ErrorProcessingCase]');"
```

Остановка:

```bash
docker compose down
# данные тома сохраняются; полный сброс:
docker compose down -v
```

Переменные: `.env` / `.env.example` (`MSSQL_SA_PASSWORD`, `MSSQL_PORT`).

## 2. Конфигурация приложения

| Файл | Назначение |
| --- | --- |
| [`appsettings.json`](../VTBL.Restrict.UI/appsettings.json) | пустые ключи (prod-шаблон) |
| [`appsettings.Development.json`](../VTBL.Restrict.UI/appsettings.Development.json) | Docker MSSQL `localhost,1434` + локальный RemoteRoot |
| `appsettings.Development.local.json` / User Secrets | свои пароли (в `.gitignore`) |

Connection string Development (должен совпадать с `.env`):

```text
Server=localhost,1434;Database=VTBL_Restrict;User Id=sa;Password=...;TrustServerCertificate=True;Encrypt=False
```

| Ключ | Назначение |
| --- | --- |
| `ConnectionStrings:RestrictDb` | непустой → SQL stores; пустой → InMemory |
| `RestrictStorage:RemoteRoot` | корень записи as-is (EC-01/08) |
| `RestrictStorage:AllowedExtensions` / `MaxFileSizeBytes` | оболочка EC-02 |
| `RabbitMq:Host` | непустой → `RabbitMqUploadNotifier`; пустой → InMemory |

Относительный `RemoteRoot` резолвится от content root приложения (каталог проекта при `dotnet run`).

LocalDB больше не дефолт: основной путь — Docker. LocalDB допустим вручную, если меняешь connection string.

## 3. Запуск UI

```bash
docker compose up -d
dotnet run --project VTBL.Restrict.UI --urls http://localhost:5000
```

Страницы:

- `/Upload` — загрузка
- `/Upload/Retry` — повтор RMQ по `correlationId`
- `/error-processing` — список Pending-кейсов (navbar «Обработка ошибок»)
- `/error-processing/{caseId}` — обработка ошибок (по `caseId`; query `token` игнорируется)

## 4. Режимы DI (кратко)

| RestrictDb | RabbitMq:Host | ListType / Batch / EP | Notifier |
| --- | --- | --- | --- |
| set | set | SQL | RabbitMQ |
| set | empty | SQL | InMemory (не live RMQ) |
| empty | * | InMemory | по Host |

## 5. Smoke

Чеклист: [`docs/smoke-checklist.md`](smoke-checklist.md).

## 6. Инварианты EC (не ослаблять)

- Файл **as is**; UI не парсит Excel/CSV.
- Сначала запись на шару/диск, затем publish RMQ.
- Error Processing только через БД (не чтение файла списка с шары).
