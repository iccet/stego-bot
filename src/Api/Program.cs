using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Formatting.Elasticsearch;
using Serilog.Sinks.Elasticsearch;

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
                .ConfigureAppConfiguration((builderContext, config) =>
                {
                    var env = builderContext.HostingEnvironment;
                    config.SetBasePath(env.ContentRootPath)
                        .AddJsonFile("appsettings.json")
                        .AddJsonFile(string.Join('.', "appsettings", env.EnvironmentName, "json"), false, false)
                        .AddEnvironmentVariables();
                })
                .UseSerilog((context, services, configuration) =>
                {
                    var host = context.Configuration.GetValue<Uri>("ELASTICSEARCH_URI");
                    configuration.ReadFrom.Configuration(context.Configuration)
                        .ReadFrom.Services(services)
                        .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(host)
                        {
                            CustomFormatter = new ElasticsearchJsonFormatter(),
                            AutoRegisterTemplate = true,
                            IndexFormat = string.Join('-', "stego", nameof(Bot)),
                            AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv6
                        });
                })
                .ConfigureLogging(logging =>
                {
                    logging.AddSerilog(dispose: true);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls("http://*:5004");
                });
    }
}