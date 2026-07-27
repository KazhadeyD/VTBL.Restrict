# docker/mssql/init

Скрипты init-контейнера (`mssql-init`). Порядок применения фиксирован в `docker-compose.yml`.

| Файл | Источник |
| --- | --- |
| `01-create-database.sql` | создание `VTBL_Restrict` |
| `02-schema.sql` | копия `docs/db/05-ddl.sql` (CREATE TABLE + seed; внутри CREATE — индексы; вне gate — идемпотентный `IX_ErrorProcessingCase_Status_CreatedAt`) |
| `03-rc-list-entry.sql` | копия `docs/db/06-rc-list-entry.sql` |
| `04-ep-index-status-createdat.sql` | зеркало `docs/db/migrations/20260715_IX_ErrorProcessingCase_Status_CreatedAt.sql` — **идемпотентный** `IF NOT EXISTS` CREATE INDEX; выполняется **всегда** (не зависит от CREATE TABLE-gate) |

При изменении DDL в `docs/db/` синхронизируй соответствующие файлы здесь (Docker Desktop не монтирует файл поверх ro-каталога `/init`).

На уже существующем volume достаточно повторно применить `04-ep-index-status-createdat.sql` (или migration из `docs/db/migrations/`) — повторный прогон безопасен.
