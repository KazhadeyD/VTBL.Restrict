using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Application.Options;
using VTBL.Restrict.Loader.Application.Uploads;
using VTBL.Restrict.Loader.Context;
using VTBL.Restrict.Loader.Infrastructure.Files;
using VTBL.Restrict.Loader.Infrastructure.Messaging;
using VTBL.Restrict.Loader.Infrastructure.Options;
using VTBL.Restrict.Loader.Infrastructure.Stub;

namespace VTBL.Restrict.Loader.Infrastructure
{
    /// <summary>
    /// Регистрация инфраструктурных зависимостей (файлы, RMQ, InMemory или EF через Context).
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddRestrictInfrastructure(this IServiceCollection services)
        {
            return services.AddRestrictInfrastructure(configuration: null);
        }

        /// <summary>
        /// ListType/Batch: EF (<see cref="RestrictDbContext"/>) при RestrictDb, иначе InMemory.
        /// Notifier: RabbitMQ при Host, иначе InMemory no-op.
        /// </summary>
        public static IServiceCollection AddRestrictInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            if (configuration != null)
            {
                services.Configure<RestrictStorageOptions>(configuration.GetSection(RestrictStorageOptions.SectionName));
                services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
            }
            else
            {
                services.Configure<RestrictStorageOptions>(_ => { });
                services.Configure<RabbitMqOptions>(_ => { });
            }

            var connectionString = configuration?.GetConnectionString("RestrictDb");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                services.AddRestrictContext(configuration);
            }
            else
            {
                services.AddSingleton<IListTypeReadStore>(_ => new InMemoryListTypeReadStore());
                services.AddSingleton<IUploadBatchStore, InMemoryUploadBatchStore>();
            }

            services.AddSingleton<IUploadPathBuilder, UploadPathBuilder>();
            services.AddSingleton<IFileShareStore, UncFileShareStore>();

            var rabbitHost = configuration?.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()?.Host;
            if (!string.IsNullOrWhiteSpace(rabbitHost))
            {
                services.AddSingleton<IUploadNotifier, RabbitMqUploadNotifier>();
            }
            else
            {
                services.AddSingleton<IUploadNotifier, InMemoryUploadNotifier>();
            }

            services.AddTransient<UploadRestrictFileCommand>();
            services.AddTransient<RetryUploadNotificationCommand>();

            return services;
        }
    }
}
