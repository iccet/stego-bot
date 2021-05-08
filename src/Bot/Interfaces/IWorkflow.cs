using System.Collections.Generic;
using System.Threading.Tasks;
using Bot.Services;
using Telegram.Bot.Types;

namespace Bot.Interfaces
{
    public interface IWorkflow
    {
        Task SendAlgorithmsList(Message message);
        Task SendDocument(CallbackQuery callbackQuery);
        Task Usage(Message message, string doc);
    }
}