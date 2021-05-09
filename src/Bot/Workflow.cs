using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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

            var bitmap = Image.FromStream(stream);

            bitmap.Save(stream, ImageFormat.Png);
            var bytes = stream.ToArray();
            
            var encoder = _encoders.GetValueOrDefault(nameof(Kutter));

            var decoded = encoder.Decode(bytes);

            var text = string.IsNullOrEmpty(decoded) ? Errors.Decode : decoded;
            await _client.SendTextMessageAsync(message.Chat.Id, text);
        }

        public async Task EncodeSource(Message message)
        {
            await _client.SendTextMessageAsync(message.Chat.Id, Strings.Decoding);
            await _client.SendChatActionAsync(message.Chat.Id, ChatAction.UploadPhoto);

            foreach (var s in message.Photo)
            {
                await using var stream = new MemoryStream();
                var file = await _client.GetFileAsync(s.FileId);
                
                await _client.DownloadFileAsync(file.FilePath, stream);

                var bitmap = Image.FromStream(stream);

                bitmap.Save(stream, ImageFormat.Png);
                var bytes = stream.ToArray();
                
                var encoder = _encoders.GetValueOrDefault("Lsb");
                encoder.Decode(bytes);


            }
            // Message[] messages;
            // await using (Stream
            //     stream1 = System.IO.File.OpenRead(Constants.PathToFile.Photos.Logo),
            //     stream2 = System.IO.File.OpenRead(Constants.PathToFile.Photos.Bot)
            // )
            // {
            //     IAlbumInputMedia[] inputMedia =
            //     {
            //         new InputMediaPhoto(new InputMedia(stream1, "logo.png"))
            //         {
            //             Caption = "Logo"
            //         },
            //         new InputMediaPhoto(new InputMedia(stream2, "bot.gif"))
            //         {
            //             Caption = "Bot"
            //         },
            //     };
            //
            //     messages = await BotClient.SendMediaGroupAsync(
            //         /* chatId: */ _fixture.SupergroupChat.Id,
            //         /* inputMedia: */ inputMedia,
            //         /* disableNotification: */ true
            //     );
            // }

            
            // const string filePath = @"Files/tux.png";
            // await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
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