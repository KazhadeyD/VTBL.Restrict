using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VTBL.Restrict.Application.Abstractions;
using VTBL.Restrict.Application.ErrorProcessing;
using VTBL.Restrict.Application.Options;
using VTBL.Restrict.Application.Uploads;
using VTBL.Restrict.Context;
using VTBL.Restrict.Infrastructure.Files;
using VTBL.Restrict.Infrastructure.Messaging;
using VTBL.Restrict.Infrastructure.Options;
using VTBL.Restrict.Infrastructure.Stub;

namespace VTBL.Restrict.Infrastructure
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
        /// ListType/Batch/EP: EF (<see cref="RestrictDbContext"/>) при RestrictDb, иначе InMemory.
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
                services.AddSingleton<IErrorProcessingStore, InMemoryErrorProcessingCaseStore>();
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
            services.AddTransient<GetErrorProcessingForOperatorQuery>();
            services.AddTransient<ResolveErrorProcessingCommand>();

            return services;
        }
    }
}
