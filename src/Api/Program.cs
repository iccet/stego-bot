using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api
{
    public class Program
    {
        public static Task Main(string[] args)
        {
            return CreateHostBuilder(args).Build().RunAsync();
        }
        
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole();
                    logging.AddFilter("Microsoft", LogLevel.Warning);
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls("http://*:5000");
                });
    }
}