# БД Restrict: предметная область

**Дата:** 14.07.2026
**Пакет:** [01-domain](01-domain.md) → [02-er](02-er.md) → [03-physical](03-physical.md) → [04-access](04-access.md) → [05-ddl.sql](05-ddl.sql)

## 1. Цель БД

Отдельная база **VTBL_Restrict** обслуживает:

1. Справочник типов рестриктивных списков.
2. (Опционально) учёт загрузок UI.
3. Кейсы **обработки ошибок**: то, что парсер не разобрал; UI показывает оператору по ссылке из письма и сохраняет правки.

БД **не** хранит сами Excel/CSV как BLOB (файлы на шаре). БД **не** является хранилищем полностью успешно распарсенных списков (это зона парсера).

## 2. Сущности

### 2.1. ListType

Тип списка (МВК, Террористы, …).

| Атрибут | Смысл |
| --- | --- |
| Code | Стабильный код |
| Name | Отображаемое имя |
| FolderSegment | Сегмент пути на шаре |
| RoutingKeySuffix | Суффикс routing key |
| IsActive | Доступен ли в UI |

**Инварианты:** `Code` уникален; неактивный тип нельзя выбрать при новой загрузке.

### 2.2. UploadBatch (рекомендуется с MVP)

Факт успешной (или частично успешной) попытки загрузки с UI.

| Атрибут | Смысл |
| --- | --- |
| CorrelationId | Связь файл ↔ RMQ ↔ логи |
| ListType | Тип |
| OriginalFileName | Имя от пользователя |
| StoredFilePath | Полный путь на шаре |
| UploadedBy | Логин/идентификатор (если есть) |
| UploadedAt | Время |
| NotifyStatus | Pending / Published / Failed |

**Инварианты:** `CorrelationId` уникален.

### 2.3. ErrorProcessingCase

Кейс ручной обработки по письму.

| Атрибут | Смысл |
| --- | --- |
| Id | PK, попадает в URL |
| ListType | Тип списка |
| AccessTokenHash | Хэш token из ссылки (сам token в БД не хранить) |
| Status | Pending / ResolvedByUser / Expired / Cancelled |
| ExpiresAt | Срок действия ссылки |
| UploadCorrelationId | Опциональная связь с загрузкой |
| SourceFilePath | Путь исходного файла (для оператора/аудита) |
| CreatedAt / CreatedBy | Парсер |
| ResolvedAt / ResolvedBy | UI / оператор |

**Инварианты:**
- Редактирование UI только при `Status = Pending` (**superseded до security-эпика:** `ExpiresAt` не используется как gate UI; колонка информативна для будущего TTL/token).
- Переход в `ResolvedByUser` только после успешного сохранения элементов.

### 2.4. ErrorProcessingItem

Проблемное поле или строка внутри кейса.

| Атрибут | Смысл |
| --- | --- |
| ErrorProcessingCaseId | FK |
| FieldCode | Код поля (или колонки) |
| RowNumber | Номер строки исходного файла (nullable) |
| RawValue | Что удалось вытащить / сырое |
| ParserMessage | Почему не разобралось |
| UserValue | Значение от оператора |
| IsRequired | Обязательность для закрытия кейса |
| SortOrder | Порядок на форме |

**Инварианты:** для `IsRequired = 1` при resolve `UserValue` не пустой (проверка UI + желательно CHECK/триггер на уровне приложения).

## 3. Статусная модель ErrorProcessingCase

```text
                    ┌──────────┐
         create     │ Pending  │◄── парсер создаёт кейс + items
                    └────┬─────┘
           expire │      │ resolve UI
                  ▼      ▼
            ┌─────────┐  ┌────────────────┐
            │ Expired │  │ ResolvedByUser │──► парсер забирает дальше
            └─────────┘  └────────────────┘
                  ▲
                  │ Cancelled — админ/парсер (редко)
```

Фоновое проставление `Expired` может делать job UI или парсер; **до security-эпика** UI при открытии/resolve проверяет только `Status` (не `ExpiresAt`).

## 4. Владение данными

| Сущность | Создаёт | Читает | Обновляет |
| --- | --- | --- | --- |
| ListType | Админ / миграции | UI, парсер | Админ |
| UploadBatch | UI | UI, поддержка | UI (NotifyStatus) |
| ErrorProcessingCase | Парсер | UI, парсер | UI (resolve), парсер (cancel/expire) |
| ErrorProcessingItem | Парсер | UI | UI (`UserValue` при resolve) |

## 5. Безопасность token

- В письме: сырой token (足够 entropy, UUID v4 или 32+ bytes random).
- В БД: только **hash** (SHA-256 / HMAC); сравнение при открытии ссылки.
- В логах: не писать сырой token.

## 6. Вне границ модели

- Очереди и таблицы парсера «боевого» списка после успешного парсинга.
- Почтовые шаблоны.
- Содержимое файла списка целиком.
