using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VTBL.Restrict.Loader.Application.Abstractions;

namespace VTBL.Restrict.Loader.Infrastructure.Stub
{
    /// <summary>
    /// Успешный no-op notifier для dev/test без RabbitMQ.
    /// </summary>
    public sealed class InMemoryUploadNotifier : IUploadNotifier
    {
        private readonly ILogger _logger;

        public InMemoryUploadNotifier(ILogger<InMemoryUploadNotifier> logger = null)
        {
            _logger = logger ?? NullLogger<InMemoryUploadNotifier>.Instance;
        }

        public Task PublishUploadedAsync(
            RestrictFileUploadedMessage message,
            string routingKey,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug(
                "In-memory upload notify completed for routing key {RoutingKey}",
                routingKey);
            return Task.CompletedTask;
        }
    }
}
