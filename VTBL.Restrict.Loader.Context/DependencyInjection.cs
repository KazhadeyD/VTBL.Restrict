using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Context.Stores;

namespace VTBL.Restrict.Loader.Context
{
    public static class DependencyInjection
    {
        /// <summary>
        /// Регистрирует <see cref="RestrictDbContext"/> и EF-реализации портов Application
        /// (ListType, UploadBatch).
        /// </summary>
        public static IServiceCollection AddRestrictContext(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration?.GetConnectionString("RestrictDb");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'RestrictDb' is required for AddRestrictContext.");
            }

            services.AddDbContext<RestrictDbContext>(options =>
                options.UseSqlServer(connectionString));

            services.AddScoped<IListTypeReadStore, EfListTypeReadStore>();
            services.AddScoped<IUploadBatchStore, EfUploadBatchStore>();

            return services;
        }
    }
}
