# Интеграция: удалённая папка + RabbitMQ

**Дата:** 14.07.2026  
**Связь:** [list-types.md](list-types.md), [db/](db/), [tz-restrict-ui.md](tz-restrict-ui.md)

## 1. Общая схема

1. UI валидирует оболочку: тип списка, расширение, размер, непустота. Содержимое файла не разбирается.
2. UI копирует файл **as is** на удалённую папку по правилам типа.
3. UI (рекомендуется) создаёт `UploadBatch` со статусом `Pending`.
4. UI публикует сообщение в RabbitMQ.
5. UI обновляет `UploadBatch.NotifyStatus` → `Published` или `Failed`.
6. Парсер consuming читает сообщение, открывает файл по `filePath`, парсит.

UI **не** вызывает HTTP API парсера.

## 2. Файловый сервер

| Параметр | Описание |
| --- | --- |
| RemoteRoot | Корень inbox, например `\\fileserver\restrict\inbox` |
| Учётка | Сервисная УЗ UI с правом write на inbox |
| Атомарность | Писать во `.tmp` / temp-имя, затем rename в финальное имя |
| Имя | `{correlationId}_{sanitizedOriginalName}` |
| Путь | `{RemoteRoot}\{FolderSegment}\{yyyy}\{MM}\{dd}\...` |

Конфиг (пример ключей `appsettings`):

```json
{
  "RestrictStorage": {
    "RemoteRoot": "\\\\fileserver\\restrict\\inbox",
    "AllowedExtensions": [ ".xlsx", ".xls", ".csv" ],
    "MaxFileSizeBytes": 52428800
  }
}
```

## 3. RabbitMQ

### 3.1. Топология (черновик)

| Элемент | Значение |
| --- | --- |
| Exchange | `restrict.uploads` (type: `topic`, durable) |
| Routing key | `restrict.upload.{RoutingKeySuffix}` |
| Queue (парсер) | `restrict.parser.uploads` (binding `#` или конкретные ключи) |
| Persistent | да |

### 3.2. Payload (JSON)

```json
{
  "messageType": "RestrictFileUploaded",
  "schemaVersion": 1,
  "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "listType": "MVK",
  "filePath": "\\\\fileserver\\restrict\\inbox\\mvk\\2026\\07\\14\\3fa85f64_list.xlsx",
  "originalFileName": "list.xlsx",
  "uploadedAtUtc": "2026-07-14T20:15:00.000Z",
  "uploadedBy": "domain\\user"
}
```

Обязательные поля: `correlationId`, `listType`, `filePath`, `uploadedAtUtc`.

### 3.3. Конфиг UI (пример)

```json
{
  "RabbitMq": {
    "Host": "rmq-host",
    "VirtualHost": "/",
    "Exchange": "restrict.uploads",
    "Username": "***",
    "Password": "***"
  }
}
```

### 3.4. Политика ошибок

| Ситуация | Поведение UI |
| --- | --- |
| Шара недоступна | Ошибка оператору; RMQ не слать; UploadBatch не создавать или Failed до publish |
| RMQ недоступен после записи файла | `NotifyStatus=Failed`; показать ошибку; опция «Повторить уведомление» публикует то же сообщение без новой копии файла |
| Дубликат publish | Парсер должен быть идемпотентен по `correlationId` |

## 4. Связь с обработкой ошибок

Парсер после проблем с полями:

1. INSERT `ErrorProcessingCase` + `ErrorProcessingItem`.
2. Генерирует данные кейса в БД (колонки token/TTL могут заполняться парсером «на будущее»).
3. Шлёт письмо со ссылкой на UI:  
   `{UiBaseUrl}/error-processing/{ErrorProcessingCaseId}`  
   (`?token=` опционален; **UI текущей реализации token не проверяет** — security epic отложен).

Обратный канал UI ← парсер по содержимому кейса: **только БД** (не RMQ для UI на MVP).

## 5. Открытые инфраструктурные точки

- Точные имена exchange/queue на стендах.
- Нужен ли dead-letter для poison messages.
- TLS/учётки RMQ и SMB.
