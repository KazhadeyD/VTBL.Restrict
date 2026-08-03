using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Loader.Application.Abstractions;

namespace VTBL.Restrict.Loader.Infrastructure.Stub
{
    /// <summary>
    /// Успешный no-op notifier для dev/test без RabbitMQ.
    /// </summary>
    public sealed class InMemoryUploadNotifier : IUploadNotifier
    {
        public Task PublishUploadedAsync(
            RestrictFileUploadedMessage message,
            string routingKey,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
