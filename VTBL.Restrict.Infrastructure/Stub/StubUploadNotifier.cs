using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Application.Abstractions;

namespace VTBL.Restrict.Infrastructure.Stub
{
    /// <summary>
    /// Заглушка публикатора RMQ (без реального брокера).
    /// </summary>
    public sealed class StubUploadNotifier : IUploadNotifier
    {
        public Task PublishUploadedAsync(RestrictFileUploadedMessage message, string routingKey, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
