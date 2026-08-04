using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace VTBL.Restrict.Loader.UI.Configuration
{
    /// <summary>
    /// Форсирует валидацию ListTypesOptions на старте приложения (fail-fast).
    /// </summary>
    public sealed class ListTypesOptionsValidationHostedService : IHostedService
    {
        private readonly IOptions<ListTypesOptions> _options;

        public ListTypesOptionsValidationHostedService(IOptions<ListTypesOptions> options)
        {
            _options = options;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = _options.Value;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}

