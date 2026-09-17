using System;

namespace VTBL.Restrict.Loader.Application.Abstractions
{
    /// <summary>
    /// Сообщение application-слоя о успешно записанном файле рестриктивного списка
    /// для публикации через <see cref="IUploadNotifier"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Это внутренний контракт Loader → нотификатор, а не JSON envelope RabbitMQ.
    /// Адаптер (например RabbitMQ) мапит поля в payload потребителя
    /// (<c>SessionId</c>, <c>UserId</c>, <c>UserName</c>, <c>FilePath</c>, <c>RequestDate</c>, <c>ListType</c>).
    /// </para>
    /// <para>
    /// <see cref="UserName"/> / <see cref="UserId"/> заполняются на UI из Windows Authentication;
    /// <see cref="UploadedBy"/> сохраняется для обратной совместимости и обычно совпадает с <see cref="UserName"/>.
    /// </para>
    /// </remarks>
    public sealed class RestrictFileUploadedMessage
    {
        /// <summary>
        /// Логический тип события (например <c>RestrictFileUploaded</c>).
        /// </summary>
        public string MessageType { get; set; }

        /// <summary>
        /// Версия схемы сообщения для эволюции контракта.
        /// </summary>
        public int SchemaVersion { get; set; }

        /// <summary>
        /// Сквозной идентификатор операции (файл на шаре, логи, SessionId в Rabbit).
        /// </summary>
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// Код типа списка (ListType), по которому выбран каталог и routing key.
        /// </summary>
        public string ListType { get; set; }

        /// <summary>
        /// Полный путь к сохранённому файлу на файловом ресурсе.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Исходное имя загруженного файла от оператора.
        /// </summary>
        public string OriginalFileName { get; set; }

        /// <summary>
        /// Время успешной записи файла в UTC.
        /// </summary>
        public DateTime UploadedAtUtc { get; set; }

        /// <summary>
        /// Кто загрузил файл (как правило Windows login, дублирует <see cref="UserName"/>).
        /// </summary>
        public string UploadedBy { get; set; }

        /// <summary>
        /// Идентификатор пользователя для потребителя: Windows SID или login без домена.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Отображаемое имя пользователя Windows (<c>DOMAIN\user</c> / UPN).
        /// </summary>
        public string UserName { get; set; }
    }
}
