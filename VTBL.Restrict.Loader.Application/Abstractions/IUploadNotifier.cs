using System;
using System.Threading;
using System.Threading.Tasks;

namespace VTBL.Restrict.Loader.Application.Abstractions
{
    /// <summary>
    /// Публикация уведомления о загрузке в RabbitMQ (после успешной записи файла).
    /// </summary>
    public interface IUploadNotifier
    {
        Task PublishUploadedAsync(RestrictFileUploadedMessage message, string routingKey, CancellationToken cancellationToken);
    }
}
