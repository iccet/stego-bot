using System.Net;
using Api.Options;
using Confluent.Kafka;
using Core.Interfaces;
using Data.Analytics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.Extensions
{
    public static class ServiceProviderExtensions
    {
        public static IServiceCollection AddKafka(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<ProducerConfig>(c =>
            {
                c.BootstrapServers = configuration["KAFKA_BOOTSTRAP_SERVERS"];
                c.ClientId = Dns.GetHostName();
            });
            
            services.Configure<ConsumerConfig>(c =>
            {
                c.BootstrapServers = configuration["KAFKA_BOOTSTRAP_SERVERS"];
                c.GroupId = "foo";
                c.ClientId = Dns.GetHostName();
                c.AutoOffsetReset = AutoOffsetReset.Earliest;
            });

            services.Configure<KafkaOptions>(o =>
            {
                o.StegoEncodeImageTopic = "stego-encoding-topic";
                o.StegoDecodeImageTopic = "stego-decoding-topic";
            });

            services.AddScoped(provider =>
            {
                var config = provider.GetRequiredService<IOptions<ProducerConfig>>();
                return new ProducerBuilder<Null, string>(config.Value).Build();
            });
            
            services.AddScoped(provider =>
            {
                var config = provider.GetRequiredService<IOptions<ProducerConfig>>();
                return new ConsumerBuilder<Ignore, string>(config.Value).Build();
            });
            
            services.AddScoped<IAnalyticsProducer, AnalyticsProducer>();
            services.AddHostedService<DecodedStegoContainerConsumer>();

            return services;
        }
    }
}