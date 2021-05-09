using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace Bot.Interfaces
{
    public interface IWorkflow
    {
        Task SendAlgorithmsList(Message message);
        Task RequestSource(CallbackQuery callbackQuery);
        Task DecodeSource(Message message);
        Task EncodeSource(Message message);
        Task Usage(Message message, string doc);
    }
}