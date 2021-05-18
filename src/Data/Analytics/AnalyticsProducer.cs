using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Options;
using Confluent.Kafka;
using Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Data.Analytics
{
    public class AnalyticsProducer : IAnalyticsProducer
    {
        private readonly IProducer<Null, string> _producer;
        private readonly KafkaOptions _options;
        private readonly ILogger<IAnalyticsProducer> _logger;

        public AnalyticsProducer(
            ILogger<IAnalyticsProducer> logger,
            IProducer<Null, string> producer,
            IOptions<KafkaOptions> options)
        {
            _logger = logger;
            _producer = producer;
            _options = options.Value;
        }

        public void Delete(Guid id)
        {
            var req = new EncodingDto<Guid>(RequestType.Decode, id)
            {
                TextId = id
            };

            Send("sss_dictionary_queue", req);
            _logger.LogInformation($"Delete dictionary {id} - sent successfully.");
        }

        private async Task Send<T>(string queueName, T obj)
        {
            try
            {
                var body = JsonSerializer.Serialize(obj);
                var message = new Message<Null, string>
                {
                    Value = body
                };
                
                await _producer.ProduceAsync(_options.StegoDecodeImageTopic, message);
            }
            catch (ProduceException<Null, string> e)
            {
                _logger.LogError(e, "Error while sending message");
            }
        }
    }
}