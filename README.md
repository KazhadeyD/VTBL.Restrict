# VTBL.Restrict.Loader

Загрузка рестриктивных списков

Workflow:
1. Выбирается тип списка и файл (обычно Excel или CSV).
2. Приложение кладёт файл на сетевую или локальную папку **как есть** — содержимое не открывает и не разбирает.
3. Дальше сообщает сервису парсинга через RabbitMQ, что файл появился.

Типы списков и папки для файлов берутся из конфигурации. Разбором содержимого занимается уже другой сервис.

## Что внутри solution

| Проект | Зачем |
| --- | --- |
| `VTBL.Restrict.Loader.sln` | Решение Visual Studio (.NET 5) |
| `VTBL.Restrict.Loader.UI` | Сайт: форма загрузки |
| `VTBL.Restrict.Loader.Application` | Сценарий «проверил → записал → уведомил» |
| `VTBL.Restrict.Loader.Domain` | Общие правила (расширения, лимиты и т.п.) |
| `VTBL.Restrict.Loader.Infrastructure` | Запись на шару, RabbitMQ, заглушки для локальной работы |
| `VTBL.Restrict.Loader.Application.Tests` / `UI.Tests` | Автотесты |
| `docker-compose.yml` | Локальный SQL (если нужен стенд вокруг, сам Loader для загрузки его не использует) |
| `docs/` | Описание продукта, БД, запуска и интеграций |

## Логи

Пишем через **NLog**. Настройки — в секции `NLog` файла `VTBL.Restrict.Loader.UI/appsettings.json`.

| Что | Куда |
| --- | --- |
| Основной лог | `C:\inetpub\logs\VTBL.Restrict.Loader.UI\YYYY-MM-DD.log` |
| Старые файлы | `C:\inetpub\logs\VTBL.Restrict.Loader.UI\archive\` (ротация раз в день или при размере больше 10 МБ, храним до 30 архивов) |
| Служебный лог NLog | `C:\inetpub\logs\VTBL.Restrict.Loader.UI\internal-nlog.txt` |
| Консоль | тот же формат строки |

Формат строки:

`дата|уровень|логгер|сообщение|CorrelationId=...|исключение`

Инцидент ищется по `CorrelationId=` (значение — с экрана оператора).

## Сообщение в RabbitMQ

После успешной записи файла уходит JSON такого вида:

```json
{
  "Method": "IllegalCompaniesLoaderProcessor",
  "Payload": "{\"SessionId\":\"<correlationId>\",\"UserId\":\"stub-user-id\",\"UserName\":\"stub-user-name\",\"FilePath\":\"<storedFilePath>\",\"AdditionalInfo\":\"stub-info\",\"RequestDate\":\"<uploadedAtUtc in ISO-8601 UTC>\"}"
}
```

Снаружи — `Method` и `Payload`. `Payload` передаётся **строкой** JSON. Поля пользователя сейчас — нейтральные заглушки.

## Как запустить локально

```bash
dotnet run --project VTBL.Restrict.Loader.UI --urls http://localhost:5000
```

В Development типы списков и папки для файлов берутся из `appsettings.Development.json` (секция `ListTypes`).

Лимит размера файла загрузки — **100 МБ** (`RestrictStorage:MaxFileSizeBytes` = `104857600`). Для IIS / IIS Express то же значение задано в `VTBL.Restrict.Loader.UI/web.config` (`maxAllowedContentLength`), иначе будет HTTP 413.1 до входа в приложение.

Если `RabbitMq:Host` пустой — в брокер ничего не шлётся, работает локальная заглушка.

## Сборка и тесты

```bash
dotnet build VTBL.Restrict.Loader.sln
dotnet test VTBL.Restrict.Loader.sln
```

## История изменений

### Сентябрь 2026
- 2026-09-10: лимит загрузки поднят до 100 МБ (`MaxFileSizeBytes`, `[RequestSizeLimit]`, `FormOptions`, `IISServerOptions`); добавлен `web.config` с `maxAllowedContentLength` для IIS / IIS Express (устранение HTTP 413.1).

### Август 2026
- 2026-08-10: удалён проект `VTBL.Restrict.Loader.Tests` (E2E); остаются `Application.Tests` и `UI.Tests`.
