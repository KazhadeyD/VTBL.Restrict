# Документация VTBL.Restrict

Пакет проектирования UI рестриктивных списков.

| Документ | Содержание |
| --- | --- |
| [tz-restrict-ui.md](tz-restrict-ui.md) | Черновик ТЗ (ранний); канон пайплайна — в локальном `docs/implementation/` |
| [list-types.md](list-types.md) | Типы списков (МВК, Террористы, …) |
| [db/](db/) | Отдельный трек проектирования БД |
| [integration-file-rmq.md](integration-file-rmq.md) | Удалённая папка + RabbitMQ |
| [screens-flow.md](screens-flow.md) | Экраны и переходы |
| [architecture.md](architecture.md) | **Утверждённая** системная архитектура (копия из этапа Архитектуры, 15.07.2026) |
| [runbook-local.md](runbook-local.md) | Локальный стенд: Docker MSSQL, appsettings, RemoteRoot, RMQ |
| [smoke-checklist.md](smoke-checklist.md) | Live smoke шаги TC-LIVE-01…06 |
| [`docker-compose.yml`](../docker-compose.yml) | MSSQL 2022 (порт 1434) + init DDL |

## БД (`docs/db`)

| Файл | Содержание |
| --- | --- |
| [01-domain.md](db/01-domain.md) | Предметная область |
| [02-er.md](db/02-er.md) | Логическая ER |
| [03-physical.md](db/03-physical.md) | Физическая модель |
| [04-access.md](db/04-access.md) | Роли и права |
| [05-ddl.sql](db/05-ddl.sql) | Базовый DDL + seed ListType |
| [06-rc-list-entry.sql](db/06-rc-list-entry.sql) | Зеркало полей Excel листа `RC` → `[restrict].[RcListEntry]` |
