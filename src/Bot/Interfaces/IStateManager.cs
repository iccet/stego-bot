using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace Bot.Interfaces
{
    public interface IStateManager
    {
        void RestoreState(Message message);
        void SaveState(Message message);
        
        ValueTask OnCallbackQueryReceived(
            object sender,
            EventArgs callbackQueryEventArgs,
            CancellationToken token);

        ValueTask OnMessageReceived(
            object sender,
            EventArgs messageEventArgs,
            CancellationToken token);
    }
}