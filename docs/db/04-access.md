# БД Restrict: матрица доступа

**Дата:** 14.07.2026

## Роли

| Роль | Кто | Назначение |
| --- | --- | --- |
| `restrict_ui` | VTBL.Restrict.UI | Загрузки, чтение справочников, resolve особых случаев |
| `restrict_parser` | Сервис парсинга | Создание кейсов и items; служебные статусы |
| `restrict_admin` | DBA / миграции | DDL, seed, админские правки справочника |

## Разрешения (логический уровень)

| Объект | restrict_ui | restrict_parser | restrict_admin |
| --- | --- | --- | --- |
| ListType | SELECT | SELECT | ALL |
| UploadBatch | INSERT, SELECT, UPDATE (NotifyStatus) | SELECT | ALL |
| SpecialCase | SELECT; UPDATE при resolve (Status, ResolvedAt, ResolvedBy) | INSERT, SELECT, UPDATE (Status Expired/Cancelled) | ALL |
| SpecialCaseItem | SELECT; UPDATE (UserValue) | INSERT, SELECT | ALL |

## Правила приложения (обязательные)

1. UI **не** создаёт `SpecialCase` / `SpecialCaseItem`.
2. UI **не** меняет `AccessTokenHash`, `ExpiresAt`, `RawValue`, `ParserMessage`, `FieldCode`.
3. Парсер **не** пишет `UserValue` и **не** ставит `ResolvedByUser` (это UI).
4. Сравнение token только через hash; сырой token в БД не хранится.
5. После `ResolvedByUser` UI не принимает повторное изменение items (read-only), пока политика не расширена.

## Рекомендуемые объекты SQL

- Роли БД: `db_restrict_ui`, `db_restrict_parser`.
- GRANT на уровне схемы `restrict` с запретом DDL для прикладных ролей.
- Для ужесточения: stored procedures `usp_SpecialCase_GetById`, `usp_SpecialCase_Resolve` под UI; парсер — `usp_SpecialCase_Create` (фаза 2, не блокер MVP).
