using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Bot.Interfaces;
using Bot.Services;
using CsStg;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

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
                    Command = Command.SetAlg,
                    Alg = k
                
                })));
            var layout = Partition(buttons, 2).ToList();
            
            layout.Add(new [] {InlineKeyboardButton.WithCallbackData(Strings.Guess)});
            
            var markup = new InlineKeyboardMarkup(layout);
            
            return _client.SendTextMessageAsync(message.Chat.Id, Strings.Choose, replyMarkup: markup);
        }

        public async Task RequestSource(CallbackQuery callbackQuery)
        {
            await _client.AnswerCallbackQueryAsync(callbackQuery.Id, Strings.UploadSource);
            await _client.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Strings.UploadSource);
        }

        public async Task DecodeSource(Message message)
        {
            await _client.SendTextMessageAsync(message.Chat.Id, Strings.Decoding);
            await _client.SendChatActionAsync(message.Chat.Id, ChatAction.UploadPhoto);

            var photoSize = message.Photo.Last();
            
            await using var stream = new MemoryStream();
            var file = await _client.GetFileAsync(photoSize.FileId);
            
            await _client.DownloadFileAsync(file.FilePath, stream);

            var bitmap = new Bitmap(stream);
            
            var encoder = _encoders.GetValueOrDefault(nameof(Lsb));
            var decoded = encoder.Decode(bitmap);

            var text = string.IsNullOrEmpty(decoded) ? Errors.Decode : decoded;
            await _client.SendTextMessageAsync(message.Chat.Id, text);
        }

        public async Task EncodeSource(Message message)
        {
            await _client.SendTextMessageAsync(message.Chat.Id, Strings.Encoding);
            await _client.SendChatActionAsync(message.Chat.Id, ChatAction.UploadPhoto);

            var photoSize = message.Photo.Last();
            await using var originalStream = new MemoryStream();
            var stream = new MemoryStream();
            var file = await _client.GetFileAsync(photoSize.FileId);
            
            await _client.DownloadFileAsync(file.FilePath, originalStream);

            var bitmap = new Bitmap(originalStream);

            var encoder = _encoders.GetValueOrDefault(nameof(Lsb));
            const string data = "Богет, богет";
            var success = encoder.Encode(data, bitmap, stream);
            
            if (success)
            {
                await _client.SendPhotoAsync(message.Chat.Id, stream, data);
            }
            else
            {
                await _client.SendTextMessageAsync(message.Chat.Id, Errors.Encode);
            }
            
            
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