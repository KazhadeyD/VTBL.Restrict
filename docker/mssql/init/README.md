# docker/mssql/init

Скрипты одноразового init-контейнера (`mssql-init`).

| Файл | Источник |
| --- | --- |
| `01-create-database.sql` | создание `VTBL_Restrict` |
| `02-schema.sql` | копия `docs/db/05-ddl.sql` |
| `03-rc-list-entry.sql` | копия `docs/db/06-rc-list-entry.sql` |

При изменении DDL в `docs/db/` синхронизируй соответствующие файлы здесь (Docker Desktop не монтирует файл поверх ro-каталога `/init`).
