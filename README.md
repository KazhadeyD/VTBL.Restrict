# VTBL.Restrict.Loader

Веб-приложение для операторов, которые загружают **рестриктивные списки** (МВК, террористы, НФД и другие).

Оператор выбирает тип списка и файл (обычно Excel или CSV). Приложение кладёт файл на сетевую или локальную папку **без изменений** — содержимое не открывает и не разбирает — и сообщает об этом сервису парсинга через RabbitMQ.

В базу при загрузке **ничего не пишем**. Типы списков и каталоги выкладки читаются из конфигурации, а разбором содержимого занимается отдельный сервис.

## Что внутри solution

| Проект | Зачем он нужен |
| --- | --- |
| `VTBL.Restrict.Loader.sln` | Решение Visual Studio (.NET 5) |
| `VTBL.Restrict.Loader.UI` | Сайт (Razor Pages): загрузка файла |
| `VTBL.Restrict.Loader.Application` | Сценарий загрузки, контракты к хранилищам |
| `VTBL.Restrict.Loader.Domain` | Общие правила и справочные типы |
| `VTBL.Restrict.Loader.Context` | Работа с SQL Server через EF Core (чтение типов) |
| `VTBL.Restrict.Loader.Infrastructure` | Запись на шару, RabbitMQ, заглушки для локальной разработки |
| `tests/…` | Автотесты |
| `docker-compose.yml` | Локальный SQL Server (порт **1434**) |
| `docs/` | Описание продукта, БД, запуска и интеграций |

Подробности — в [документации](docs/README.md). Быстрый старт: [локальный стенд](docs/runbook-local.md). Проверка руками: [smoke-чеклист](docs/smoke-checklist.md).

## Логи

Провайдер — **NLog** (`NLog.Web.AspNetCore`), конфиг в секции `NLog` файлов `VTBL.Restrict.Loader.UI/appsettings*.json`.

| Окружение | Куда пишем |
| --- | --- |
| Production (базовый `appsettings.json`) | файл `{basedir}/logs/app-YYYY-MM-DD.log` |
| Development | тот же файл **и** консоль |

Ротация: ежедневно и/или при размере файла > 10 МБ; архивы в `logs/archive/` (до 30 файлов в prod, до 14 в Development). Каталог `logs/` в `.gitignore`.

Поля scope (колонки layout, не дублируются в тексте сообщения):

| Поле | Смысл |
| --- | --- |
| `operation` | `upload-http` / `upload` / `publish` |
| `correlationId` | id загрузки (после генерации) |
| `listType` | код типа списка |
| `requestId` | `HttpContext.TraceIdentifier` на границе UI |

Типичные сообщения: `Upload started/succeeded/failed`, `File share write …`, `RabbitMQ publish …`, `Upload HTTP POST …`. При ошибках в лог уходит exception. Искать инцидент: по `corr=` / `correlationId` в файле лога.

## Формат сообщения в RabbitMQ

Публикуется JSON-объект следующего вида:

```json
{
  "Method": "IllegalCompaniesLoaderProcessor",
  "Payload": "{\"SessionId\":\"<correlationId>\",\"UserId\":\"stub-user-id\",\"UserName\":\"stub-user-name\",\"FilePath\":\"<storedFilePath>\",\"AdditionalInfo\":\"stub-info\",\"RequestDate\":\"<uploadedAtUtc in ISO-8601 UTC>\"}"
}
```

`Payload` передаётся строкой JSON, как требует потребитель.

## Как запустить

```bash
copy .env.example .env
docker compose up -d

dotnet run --project VTBL.Restrict.Loader.UI --urls http://localhost:5000
```

В Development приложение ходит в SQL на `localhost,1434`, база `VTBL_Restrict` (пароль — в `.env`). Если `RabbitMq:Host` пустой, сообщения в брокер не уходят — используется локальная заглушка.

## Сборка и тесты

```bash
dotnet build VTBL.Restrict.Loader.sln
dotnet test VTBL.Restrict.Loader.sln
```

## Кратко по истории

### Август 2026
- 2026-08-04: из `RestrictStorage` / `RestrictStorageOptions` убран мёртвый `RemoteRoot`; каталог выкладки только в `ListTypes[].remoteRoot`, секция `RestrictStorage` — расширения и лимит размера.
- 2026-08-04: логирование доведено до рабочего уровня: NLog file (+ console в Dev) с archive, exception в catch, логи шары/RabbitMQ/UI POST, поля `operation/correlationId/listType/requestId` через `BeginScope` без дублей в тексте сообщения.
- 2026-08-04: формат RabbitMQ-сообщения приведён к контракту `Method` + строковый `Payload`; `UserId`, `UserName`, `AdditionalInfo` заполняются нейтральными заглушками.
- 2026-08-04: production-код приведён к правилу “1 файл – 1 top-level тип” (разнесены интерфейс/DTO и запрос/результат/результат валидатора по отдельным `.cs`).
- 2026-08-04: подключён NLog для текстового логирования; конфиг секции `NLog` вынесен в `UI/appsettings*.json` с выводом `operation/correlationId/listType` из `BeginScope`.
- 2026-08-04: `ListTypes` переведён на чтение из `UI/appsettings.json` (ключ `ListTypes` как массив); минимальный контракт типа списка сжат до `remoteRoot`, `code`, `name`, а `remoteRoot` теперь считается полным корнем выкладки для конкретного типа.
- 2026-08-04: `BuildTargetPath` упрощён до формата `{RemoteRoot}\{correlationIdN}_{sanitizedOriginalName}` (без вложенности по дате).
- 2026-08-04: архитектура усилена под `net5`: убрана прямая зависимость `UI -> Context`, `AppSettingsListTypeReadStore` отвязан от `IConfiguration`, добавлена fail-fast валидация `ListTypes` на старте (обязательность `remoteRoot/code/name`, уникальность `code`, допустимый формат `code`).
- 2026-08-04: fail-fast валидация `ListTypes` переведена на альтернативный вариант через `Options` + `IValidateOptions` + `IHostedService` (валидация на старте без раздувания `Startup`).
- Проект переименован в **VTBL.Restrict.Loader**.
- Из продукта убрана обработка ошибок парсинга.
- Загрузка больше не пишет в БД (только шара + RabbitMQ); retry уведомления убран.
- В интерфейсе убрана Privacy, обновлены цвета и сброс файла.

### Июль 2026
- Собран модульный .NET 5-проект: загрузка файла, шара, RabbitMQ, Docker MSSQL.
- Добавлен тип списка NFA; доступ к БД переведён на EF Core.
