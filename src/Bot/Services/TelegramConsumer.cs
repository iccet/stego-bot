using System;
using System.Threading;
using System.Threading.Tasks;
using Bot.Interfaces;
using Bot.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;

namespace Bot.Services
{
    using StateMachine = Stateless.StateMachine<State, Command>;
    
    public sealed class TelegramConsumer : IPostService, IHostedService
    {
        #region Fields
        
        private readonly ITelegramBotClient _client;
        private readonly ILogger<IPostService> _logger;
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly IServiceProvider _provider;
        private bool _disposed;

        #endregion

        #region IDisposable

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
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
            IBackgroundTaskQueue taskQueue, 
            IServiceProvider provider)
        {
            _client = client;
            _logger = logger;
            _taskQueue = taskQueue;
            _provider = provider;
        }

        #endregion

        #region Public
        public Task StartAsync(CancellationToken cancellationToken)
        {
            Subscribe();
            _client.StartReceiving(Array.Empty<UpdateType>(), cancellationToken);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
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
            var manager = _provider.GetRequiredService<IStateManager>();
            manager.RestoreState(messageEventArgs.Message);
            
            await _taskQueue.QueueAsync(manager.OnMessageReceived, sender, messageEventArgs);
        }

        private async void OnCallbackQueryReceived(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var manager = _provider.GetRequiredService<IStateManager>();
            manager.RestoreState(callbackQueryEventArgs.CallbackQuery.Message);
            
            await _taskQueue.QueueAsync(manager.OnCallbackQueryReceived, sender, callbackQueryEventArgs);
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

        private void Subscribe()
        {
            _client.OnMessage += OnMessageReceived;
            _client.OnMessageEdited += OnMessageReceived;
            _client.OnCallbackQuery += OnCallbackQueryReceived;
            _client.OnInlineQuery += OnInlineQueryReceived;
            _client.OnInlineResultChosen += OnChosenInlineResultReceived;
            _client.OnReceiveError += OnReceiveError;
        }
        #endregion
    }
}