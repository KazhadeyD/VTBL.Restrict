using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Application.Options;
using VTBL.Restrict.Loader.Application.Uploads;
using VTBL.Restrict.Loader.Infrastructure.Files;
using VTBL.Restrict.Loader.Infrastructure;
using VTBL.Restrict.Loader.Infrastructure.Messaging;
using VTBL.Restrict.Loader.Infrastructure.Options;
using VTBL.Restrict.Loader.Infrastructure.Stub;
using VTBL.Restrict.Loader.UI.Configuration;

namespace VTBL.Restrict.Loader.UI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            var maxFileSizeBytes = Configuration.GetValue<long?>("RestrictStorage:MaxFileSizeBytes") ?? 104_857_600L;

            services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = maxFileSizeBytes;
            });

            // IIS / IIS Express: иначе MaxRequestBodySize по умолчанию ~30 МБ режет тело после requestFiltering.
            services.Configure<IISServerOptions>(options =>
            {
                options.MaxRequestBodySize = maxFileSizeBytes;
            });

            services.AddRazorPages();

            services.Configure<RestrictStorageOptions>(Configuration.GetSection(RestrictStorageOptions.SectionName));
            services.Configure<RabbitMqOptions>(Configuration.GetSection(RabbitMqOptions.SectionName));

            // ListTypes берем из конфигурации, БД игнорируем.
            services.AddOptions<ListTypesOptions>();
            services.AddSingleton<IConfigureOptions<ListTypesOptions>, ListTypesOptionsSetup>();
            services.AddSingleton<IValidateOptions<ListTypesOptions>, ListTypesOptionsValidator>();
            services.AddHostedService<ListTypesOptionsValidationHostedService>();
            services.AddSingleton<IListTypeReadStore>(sp =>
                new AppSettingsListTypeReadStore(sp.GetRequiredService<IOptions<ListTypesOptions>>().Value.Items));

            services.AddSingleton<IUploadPathBuilder, UploadPathBuilder>();
            services.AddSingleton<IFileShareStore, UncFileShareStore>();

            var rabbitHost = Configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()?.Host;
            if (!string.IsNullOrWhiteSpace(rabbitHost))
            {
                services.AddSingleton<IUploadNotifier, RabbitMqUploadNotifier>();
            }
            else
            {
                services.AddSingleton<IUploadNotifier, InMemoryUploadNotifier>();
            }

            services.AddTransient<UploadRestrictFileCommand>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
            });
        }
    }
}
