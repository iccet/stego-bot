using System;
using System.Text;
using System.Text.Json;
using Core.Interfaces;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Data.Analytics
{
    public class AnalyticsProducer : IAnalyticsProducer
    {
        private readonly IConnection _connection;
        private readonly ILogger<AnalyticsProducer> _logger;

        public AnalyticsProducer(
            ILogger<AnalyticsProducer> logger, 
            IConnection connection)
        {
            _logger = logger;
            _connection = connection;
        }

        public void DictionaryDelete(Guid id)
        {
            var req = new AnalyticsDto<Guid>(RequestType.Decode, id)
            {
                TextId = id
            };

            Send("sss_dictionary_queue", req);
            _logger.LogInformation($"Delete dictionary {id} - sent successfully.");
        }

        private void Send<T>(string queueName, T obj)
        {
            using var channel = _connection.CreateModel();
            channel.QueueDeclare(queueName, true, false, false, null);
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));
            channel.BasicPublish("", queueName, null, body);
            channel.Close();
        }
    }
}