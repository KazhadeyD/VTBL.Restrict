using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Application.Abstractions;

namespace VTBL.Restrict.Infrastructure.Stub
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
