using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Interfaces
{
    public interface IBackgroundTaskQueue
    {
        public delegate ValueTask TelegramTask(object sender, EventArgs args, CancellationToken token);
        
        ValueTask QueueAsync(TelegramTask workItem, object sender, EventArgs args);
    
        ValueTask<(TelegramTask, object, EventArgs)> DequeueAsync(
            CancellationToken cancellationToken);
    }
}