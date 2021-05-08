using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bot.Interfaces;
using Bot.Services;
using CsStg;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using StateMachine = Stateless.StateMachine<Bot.Services.State, Bot.Services.Command>;

namespace Bot
{
    public class Workflow : IWorkflow
    {
        private readonly ITelegramBotClient _client;
        private readonly Dictionary<string, AbstractEncoder> _encoders;
        
        public Workflow(
            ITelegramBotClient client,
            IEnumerable<AbstractEncoder> encoders)
        {
            _client = client;
            _encoders = encoders.ToDictionary(e => e.GetType().Name, e => e);
        }

        public Task SendAlgorithmsList(Message message)
        {
            var buttons = _encoders.Keys.Select(k => InlineKeyboardButton.WithCallbackData(k, 
                JsonSerializer.Serialize(
                new Callback
                {
                    Command = Command.Input,
                    Id = Guid.NewGuid()
                
                })));
            var layout = Partition(buttons, 2).ToList();
            
            layout.Add(new [] {InlineKeyboardButton.WithCallbackData(Strings.Guess)});
            
            var markup = new InlineKeyboardMarkup(layout);
            
            return _client.SendTextMessageAsync(message.Chat.Id, Strings.Choose, replyMarkup: markup);
        }

        public async Task SendDocument(CallbackQuery callbackQuery)
        {
            await _client.AnswerCallbackQueryAsync(callbackQuery.Id, Strings.UploadSource);
            await _client.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Strings.UploadSource);

            await _client.SendChatActionAsync(callbackQuery.Message.Chat.Id, ChatAction.UploadPhoto);
            
            const string filePath = @"Files/tux.png";
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fileName = filePath.Split(Path.DirectorySeparatorChar).Last();
            await _client.SendPhotoAsync(callbackQuery.Message.Chat.Id,new InputOnlineFile(fileStream, fileName),"Nice Picture");
        }
        
        public Task Usage(Message message, string doc)
        {
            return _client.SendTextMessageAsync(message.Chat.Id, doc, replyMarkup: new ReplyKeyboardRemove());
        }

        private static IEnumerable<IEnumerable<T>> Partition<T>(IEnumerable<T> e, int p)
        {
            var enumerator = e.GetEnumerator();
            int i = 0;
            while (enumerator.MoveNext())
            {
                yield return e.Skip(p * i++).Take(p);
            }
        }
        
    }
}