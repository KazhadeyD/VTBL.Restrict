using System;
using System.Threading;
using System.Threading.Tasks;

namespace VTBL.Restrict.Application.Abstractions
{
    /// <summary>
    /// Публикация RestrictFileUploaded в RabbitMQ (после успешной записи файла — EC-08).
    /// </summary>
    public interface IUploadNotifier
    {
        Task PublishUploadedAsync(RestrictFileUploadedMessage message, string routingKey, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Payload уведомления о загрузке файла.
    /// </summary>
    public sealed class RestrictFileUploadedMessage
    {
        public string MessageType { get; set; }
        public int SchemaVersion { get; set; }
        public Guid CorrelationId { get; set; }
        public string ListType { get; set; }
        public string FilePath { get; set; }
        public string OriginalFileName { get; set; }
        public DateTime UploadedAtUtc { get; set; }
        public string UploadedBy { get; set; }
    }
}
