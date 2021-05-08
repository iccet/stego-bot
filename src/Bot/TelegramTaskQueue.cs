using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bot.Interfaces;

namespace Bot
{
    public class TelegramTaskQueue : IBackgroundTaskQueue
    {
        private readonly Channel<(IBackgroundTaskQueue.TelegramTask, object, EventArgs)> _queue;
    
        public TelegramTaskQueue(int capacity)
        {
            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            _queue = Channel.CreateBounded<(IBackgroundTaskQueue.TelegramTask, object, EventArgs)>(options);
        }
    
        public ValueTask QueueAsync(
            IBackgroundTaskQueue.TelegramTask workItem,
            object sender,
            EventArgs args)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }
    
            return _queue.Writer.WriteAsync((workItem, sender, args));
        }

        public ValueTask<(IBackgroundTaskQueue.TelegramTask, object, EventArgs)> DequeueAsync(
            CancellationToken cancellationToken)
        {
            return _queue.Reader.ReadAsync(cancellationToken);
        }
    }
}