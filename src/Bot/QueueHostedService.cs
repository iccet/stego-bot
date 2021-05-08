using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bot.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bot
{
    public class QueuedHostedService : BackgroundService
    {
        private readonly ILogger<QueuedHostedService> _logger;

        public QueuedHostedService(IBackgroundTaskQueue taskQueue, 
            ILogger<QueuedHostedService> logger)
        {
            TaskQueue = taskQueue;
            _logger = logger;
        }

        private IBackgroundTaskQueue TaskQueue { get; }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is running.");
            return BackgroundProcessing(stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var (task, sender, args) = await TaskQueue.DequeueAsync(stoppingToken);
                var code = task.GetHashCode();
                
                _logger.LogInformation("Queued Background Task {Code} is starting.", code);

                try
                {
                    await task(sender, args, stoppingToken);
                    _logger.LogInformation("Queued Background Task {Code} is complete.", code);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing {task}.", nameof(task));
                }
            }
        }

        public override Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is stopping.");

            return base.StopAsync(stoppingToken);
        }
    }
}