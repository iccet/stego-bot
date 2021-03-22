#nullable enable
using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Interfaces;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Data.Analytics
{
    public class AnalyticsConsumer : IAnalyticsConsumer
    {
        private readonly IModel _channel;
        private readonly IConnection _connection;
        private readonly JsonSerializerOptions _options;
        private readonly ILogger<IAnalyticsConsumer> _logger;

        public AnalyticsConsumer(
            IConnection connection,
            ILogger<AnalyticsConsumer> logger)
        
        {
            _connection = connection;
            _channel = _connection.CreateModel();
            _logger = logger;
            _options = new JsonSerializerOptions
            {
                Converters =
                {
                    new JsonStringEnumConverter()
                },
                PropertyNamingPolicy = new JsonSnakeCaseNamingPolicy()
            };
        }
        
        public void Subscribe()
        {
            _channel.QueueDeclare("result_queue", true, false, false, null);
            
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += Consume;

            _channel.BasicConsume("result_queue", true, consumer);
        }
        
        public void Unsubscribe()
        {
            _connection.Close();
        }

        private void Consume(object? model, BasicDeliverEventArgs args)
        {
            var message = Encoding.UTF8.GetString(args.Body.ToArray());
            _logger.LogDebug(message);

            try
            {
                var result = JsonSerializer.Deserialize<EncodingRequest>(message, _options);

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
}