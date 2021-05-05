using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bot.Interfaces;
using Bot.Types;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using StateMachine = Stateless.StateMachine<Bot.Services.State, Bot.Services.Command>;

namespace Bot.Services
{
    public sealed class TelegramConsumer : IPostService, IHostedService
    {
        #region Types
        [Serializable]
        private struct Callback
        {
            public Guid Id { get; set; }
            public Command Command { get; set; }
        }
        #endregion
        
        #region Fields

        private delegate string DocString();
        private readonly Dictionary<Command, DocString> _commandDocs;
        private const string CommandDocFormat = "/{0} : {1}";

        private readonly StateMachine _machine;
        private readonly Dictionary<Command, StateMachine.TriggerWithParameters<Message, CallbackQuery>> _params;
        private readonly ITelegramBotClient _client;
        private readonly IDistributedCache _cache;
        private readonly ILogger<IPostService> _logger;
        private readonly CancellationTokenSource _token;
        private bool _disposed;

        #endregion

        #region IDisposable

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _token.Cancel();
                    _client.StopReceiving();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region .ctors

        public TelegramConsumer(
            ITelegramBotClient client,
            ILogger<IPostService> logger,
            IDistributedCache cache)
        {
            _commandDocs = new Dictionary<Command, DocString>
            {
                {Command.Help, () => Commands.Help},
                {Command.Inline, () => Commands.Inline},
                {Command.Start, () => Commands.Start},
                {Command.Encode, () => Commands.Encode},
                {Command.Input, () => Commands.Input},
                {Command.Decode, () => Commands.Decode},
            };
            _machine = new StateMachine(State.Idle);
            _params = new Dictionary<Command, StateMachine.TriggerWithParameters<Message, CallbackQuery>>();
            _client = client;
            _logger = logger;
            _cache = cache;
            _token = new CancellationTokenSource();
        }

        #endregion

        #region Public
        public Task StartAsync(CancellationToken cancellationToken)
        {
            Subscribe();
            ConfigureStateMachine();
            _client.StartReceiving(Array.Empty<UpdateType>(), _token.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _token.Cancel();
            _client.StopReceiving();
            return Task.CompletedTask;
        }
        #endregion

        #region Private

        private static string BuildDictionaryType(DictionaryTitle dictionary)
        {
            switch (dictionary.Type)
            {
                case DictionaryType.neutral: return "😐";
                case DictionaryType.positive: return "😃";
                case DictionaryType.negative: return "😡";
                default: return "😕";
            }
        }
        
        #region Delegates 

        private async void OnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;
            var scope = new Dictionary<string, object>
            {
                {"UserId", message.From.Username},
                {"Event", nameof(OnMessageReceived)}
            };

            Commands.Culture = CultureInfo.GetCultureInfo(
#if  DEBUG
            "ru"      
#else
            messageEventArgs.Message.From.LanguageCode
#endif
            );

            if (message == null || message.Type != MessageType.Text) return;

            var result = Enum.TryParse(message.Text
                .Split(' ')
                .First()[1..]
                .Trim(), true, out Command command);

            if (!result) command = Command.Input;

            var param = Param(command);
            
            using (_logger.BeginScope(scope))
            {
                try
                {
                    await _machine.FireAsync(param, message, null);
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message);
                
                    param = Param(Command.Help);
                    await _machine.FireAsync(param, message, null);
                }
                _logger.LogInformation("Current state {State}", _machine.State);
            }
        }

        private async void OnCallbackQueryReceived(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var query = callbackQueryEventArgs.CallbackQuery;
            try
            {
                var param = Param(Command.Input);
                await _machine.FireAsync(param, null, query);
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
            }
        }
        
        private async void OnInlineQueryReceived(object sender, InlineQueryEventArgs inlineQueryEventArgs)
        {
            _logger.LogInformation($"Received inline query from: {inlineQueryEventArgs.InlineQuery.From.Id}");

            InlineQueryResultBase[] results = {
                new InlineQueryResultArticle("3", "Tg_clients", new InputTextMessageContent("hello"))
            };
            await _client.AnswerInlineQueryAsync(inlineQueryEventArgs.InlineQuery.Id, results, isPersonal: true, cacheTime: 0);
        }

        private void OnChosenInlineResultReceived(object sender, ChosenInlineResultEventArgs chosenInlineResultEventArgs)
        {
            _logger.LogInformation($"Received inline result: {chosenInlineResultEventArgs.ChosenInlineResult.ResultId}");
        }

        private void OnReceiveError(object sender, ReceiveErrorEventArgs receiveErrorEventArgs)
        {
            using(_logger.BeginScope("Telegram Bot"))
            {
                _logger.LogError("Received error: {0} — {1}",
                    receiveErrorEventArgs.ApiRequestException.ErrorCode,
                    receiveErrorEventArgs.ApiRequestException.Message);
            }
        }
        #endregion

        #region Actions

        private Task SendReplyKeyboard(Message message)
        {
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(
                new[]
                {
                    new[] { KeyboardButton.WithRequestLocation("Location"), "1" },
                    new[] { KeyboardButton.WithRequestContact("Contact"), "2" },
                },
                true
            );

            return _client.SendTextMessageAsync(message.Chat.Id, Strings.Choose, replyMarkup: replyKeyboardMarkup);
        }

        private async Task SendDocument(Message message)
        {
            await _client.SendChatActionAsync(message.Chat.Id, ChatAction.UploadPhoto);

            const string filePath = @"Files/tux.png";
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fileName = filePath.Split(Path.DirectorySeparatorChar).Last();
            await _client.SendPhotoAsync(message.Chat.Id,new InputOnlineFile(fileStream, fileName),"Nice Picture");
        }
        
        #region Usage
        private string BuildDoc(IEnumerable<Command> triggers)
        {
            return string.Join('\n', triggers.Select(t => string.Format(
                CommandDocFormat, 
                t.ToString("G").ToLower(),
                _commandDocs[t]())));
        }
        
        private Task Usage(Message message)
        {
            return Usage(message, BuildDoc(_machine.PermittedTriggers));
        }

        private Task Usage(Message message, string doc)
        {
            return _client.SendTextMessageAsync(message.Chat.Id, doc, replyMarkup: new ReplyKeyboardRemove());
        }
        #endregion

        private async Task Input(CallbackQuery query)
        {
            var callback = JsonSerializer.Deserialize<Callback>(query.Data);

            await _client.AnswerCallbackQueryAsync(query.Id);
        }
        #endregion

        private void Subscribe()
        {
            _client.OnMessage += OnMessageReceived;
            _client.OnMessageEdited += OnMessageReceived;
            _client.OnCallbackQuery += OnCallbackQueryReceived;
            _client.OnInlineQuery += OnInlineQueryReceived;
            _client.OnInlineResultChosen += OnChosenInlineResultReceived;
            _client.OnReceiveError += OnReceiveError;
        }

        private void ConfigureTriggerParameters()
        {
            foreach (var c in (Command[]) Enum.GetValues(typeof(Command)) )
            {
                var trigger = _machine.SetTriggerParameters<Message, CallbackQuery>(c);
                _params.Add(c, trigger);
            }
        }

        private void ConfigureStateMachine()
        {
            ConfigureTriggerParameters();
            
            var idle = _machine.Configure(State.Idle)
                .PermitReentry(Command.Start)
                .PermitReentry(Command.Input)
                .OnEntryFrom(Param(Command.Start), (message, query) => Usage(message))
                
                .Permit(Command.Inline, State.Inline);

            var inline = _machine.Configure(State.Inline)
                .OnEntryFromAsync(Param(Command.Inline), 
                    async (m, q) => await SendReplyKeyboard(m));
	
            var encoding = _machine.Configure(State.Source)
                .PermitReentry(Command.Input)
                .PermitReentry(Command.Encode)
                .PermitReentry(Command.Decode)
                .OnEntryFromAsync(Param(Command.Input), 
                    async (m, q) => await Input(q));
	
            var common = new[] {encoding, inline, idle};
            foreach (var conf in common)
            {
                conf.PermitReentry(Command.Help)
                    .OnEntryFrom(Param(Command.Help),
                        async (message, query) => await Usage(message));
            }
            
            foreach (var conf in common.Take(common.Length - 1))
            {
                conf.Permit(Command.Start, State.Idle);
            }
        }

        private StateMachine.TriggerWithParameters<Message, CallbackQuery> Param(params Command[] command) => 
            _params.FirstOrDefault(p => command.Any(c => p.Key == c)).Value;
        
        #endregion
    }
}