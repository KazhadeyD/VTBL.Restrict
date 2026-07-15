using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Application.Abstractions;

namespace VTBL.Restrict.Tests.Fakes
{
    /// <summary>
    /// Симуляция сбоя publish RMQ.
    /// </summary>
    public sealed class FailingUploadNotifier : IUploadNotifier
    {
        public Task PublishUploadedAsync(
            RestrictFileUploadedMessage message,
            string routingKey,
            CancellationToken cancellationToken)
        {
            throw new System.InvalidOperationException("Simulated RMQ publish failure.");
        }
    }
}
