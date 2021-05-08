using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bot.Interfaces;
using Bot.Types;
using CsStg;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        
        #region Fields
        private readonly Dictionary<string, AbstractEncoder> _encoders;
        private readonly ITelegramBotClient _client;
        private readonly IDistributedCache _cache;
        private readonly ILogger<IPostService> _logger;
        private readonly DocBuilder _doc;
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
            IDistributedCache cache,
            IEnumerable<AbstractEncoder> encoders,
            IBackgroundTaskQueue taskQueue, 
            IServiceProvider provider)
        {
            _encoders = encoders.ToDictionary(e => e.GetType().Name, e => e);
            _doc = new DocBuilder();
            _client = client;
            _logger = logger;
            _cache = cache;
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
            var manager = _provider.GetRequiredService<StateManager>();
            manager.RestoreState(messageEventArgs.Message);
            
            await _taskQueue.QueueAsync(manager.OnMessageReceived, sender, messageEventArgs);
        }

        private async void OnCallbackQueryReceived(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var query = callbackQueryEventArgs.CallbackQuery;
            var machine = new StateMachine(State.Idle);
            try
            {
                // var param = Param(Command.Input);
                // await machine.FireAsync(param, null, query);
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