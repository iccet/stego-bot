#nullable enable
using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Api.Options;
using Confluent.Kafka;
using Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Data.Analytics
{
    delegate string EncodingTopic();
    
    public class DecodedStegoContainerConsumer : BackgroundService
    {
        private readonly KafkaOptions _kafkaOptions;
        
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly ILogger<IHostedService> _logger;
        private readonly IConsumer<Ignore, string> _consumer;

        public DecodedStegoContainerConsumer(
            ILogger<DecodedStegoContainerConsumer> logger,
            IOptions<KafkaOptions> options,
            IConsumer<Ignore, string> consumer,
            IOptions<JsonSerializerOptions> serializerOptions)

        {
            _logger = logger;
            _consumer = consumer;
            _serializerOptions = serializerOptions.Value;
            _kafkaOptions = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumer = _consumer.Consume(stoppingToken);
                    
                    _logger.LogInformation("Message: {Value} received from {TopicPartitionOffset}",
                        consumer.Message.Value,
                        consumer.TopicPartitionOffset);
                    
                    var result = JsonSerializer.Deserialize<EncodingRequest>(
                        consumer.Message.Value, 
                        _serializerOptions);

                    switch (result.EncodingResult.Code)
                    {
                        case HttpStatusCode.InternalServerError:
                            _logger.LogError("{Code}", result.EncodingResult.Code);
                            break;
                        case HttpStatusCode.OK:
                            _logger.LogInformation("{Code}", result.EncodingResult.Code);
                            break;
                        case HttpStatusCode.Accepted:
                            _logger.LogWarning("{Code}", result.EncodingResult.Code);
                            break;
                        default:
                            _logger.LogWarning("{Code}", result.EncodingResult.Code);
                            break;
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message);
                }
            }
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _consumer.Subscribe(_kafkaOptions.StegoDecodeImageTopic);
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _consumer.Close();
            return base.StopAsync(cancellationToken);
        }
    }
}