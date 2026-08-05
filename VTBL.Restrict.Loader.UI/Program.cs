using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;

namespace VTBL.Restrict.Loader.UI
{
    /// <summary>
    /// Точка входа приложения.
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                CreateHostBuilder(args).Build().Run();
            }
            finally
            {
                // NLog умеет буферить и писать асинхронно, поэтому на завершении процесса нужно “смыть” всё в файл.
                NLog.LogManager.Shutdown();
            }
        }

        /// <summary>
        /// Поднимает хост и подключает NLog как единственный provider логов.
        /// </summary>
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureLogging(logging => logging.ClearProviders())
                .UseNLog();
    }
}
