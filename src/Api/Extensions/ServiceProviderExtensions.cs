using Core.Interfaces;
using Data.Analytics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Api.Extensions
{
    public static class ServiceProviderExtensions
    {
        public static IServiceCollection AddRabbit(this IServiceCollection services, IConfiguration configuration)
        {
            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMq_Host"]
            };
            
            services.AddScoped(provider => factory.CreateConnection());
            
            services.AddSingleton<IAnalyticsProducer, AnalyticsProducer>();
            services.AddSingleton<IAnalyticConsumer, AnalyticsConsumer>();

            return services;
        }
    }
}