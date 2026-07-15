# Типы рестриктивных списков

**Дата:** 14.07.2026  
**Назначение:** справочник кодов для UI, БД (`ListType`), пути файла и RabbitMQ routing.

## 1. Принципы

- Тип выбирает **оператор** явно при загрузке; UI не угадывает тип по содержимому файла и вообще **не читает** содержимое — файл уходит as is.
- Новый тип = строка в справочнике + правила маршрутизации, без смены UC-01.
- `Code` стабилен (латиница/underscore), `Name` — для UI.

## 2. Каталог (MVP+)

| Code | Name (UI) | Описание | FolderSegment | RoutingKeySuffix | MVP |
| --- | --- | --- | --- | --- | --- |
| `MVK` | МВК | Список МВК | `mvk` | `mvk` | да |
| `TERRORISTS` | Террористы | Список террористов | `terrorists` | `terrorists` | да |
| `OTHER` | Прочее | Резерв / неклассифицированные до уточнения | `other` | `other` | опционально |

Расширение: добавлять строки в таблицу `restrict.ListType` и конфигурацию пути (см. integration). Коды ниже — кандидаты; **не активировать** без согласования с парсером:

| Code | Name | Примечание |
| --- | --- | --- |
| `UN_SANCTIONS` | Санкции ООН | placeholder |
| `OFAC` | OFAC | placeholder |
| `NATIONAL` | Национальный перечень | placeholder |

## 3. Правила маршрутизации файла

Базовый корень (конфиг): `{RemoteRoot}`  

Целевой путь:

```text
{RemoteRoot}\{FolderSegment}\{yyyy}\{MM}\{dd}\{correlationId}_{originalFileName}
```

Пример:

```text
\\fileserver\restrict\inbox\mvk\2026\07\14\a1b2c3d4_list.xlsx
```

- `{correlationId}` — GUID без дефисов или короткий hex (единый с RMQ и опциональным `UploadBatch`).
- Имя файла санитизируется (запрет `..`, `\`, `/`).

## 4. Правила RabbitMQ

- Routing key (черновик): `restrict.upload.{RoutingKeySuffix}`  
  Пример: `restrict.upload.mvk`
- В теле сообщения обязательно поле `listType` = `Code` из таблицы выше.

## 5. Связь с БД

Справочник хранится в `restrict.ListType` (см. [db/03-physical.md](db/03-physical.md)).  
UI читает только `IsActive = 1`.  
`ErrorProcessingCase.ListTypeId` ссылается на этот справочник.

## 6. Открытые уточнения

- Финальный перечень типов prod.
- Нужен ли отдельный routing key на стенд (`dev`/`test`) префиксом.
- Допустимые расширения **по типу** или общие для всех.
