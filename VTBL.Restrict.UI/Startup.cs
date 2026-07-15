using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VTBL.Restrict.Infrastructure;

namespace VTBL.Restrict.UI
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
            var maxFileSizeBytes = Configuration.GetValue<long?>("RestrictStorage:MaxFileSizeBytes") ?? 52_428_800L;

            services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = maxFileSizeBytes;
            });

            services.AddRazorPages(options =>
            {
                // Канонический маршрут загрузки: /Upload
            });

            services.AddRestrictInfrastructure(Configuration);
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
