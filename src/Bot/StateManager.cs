using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bot.Services;
using CsStg;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using StateMachine = Stateless.StateMachine<Bot.Services.State, Bot.Services.Command>;

namespace Bot
{
    public class StateManager
    {
        private delegate string StateKey(Message message);
        private readonly StateKey _stateKey = message => message.From.Id.ToString();
        
        private readonly IDistributedCache _cache;
        private readonly ITelegramBotClient _client;
        private readonly ILogger<StateManager> _logger;
        private readonly DocBuilder _doc;
        private readonly Dictionary<string, AbstractEncoder> _encoders;
        private readonly List<StateMachine.TriggerWithParameters<Message>> _messageTriggers;
        private readonly List<StateMachine.TriggerWithParameters<CallbackQuery>> _callbackTriggers;

        private StateMachine _machine;
        
        public StateManager(
            IDistributedCache cache,
            ITelegramBotClient client,
            ILogger<StateManager> logger,
            IEnumerable<AbstractEncoder> encoders)
        {
            _cache = cache;
            _client = client;
            _logger = logger;
            _doc = new DocBuilder();
            _encoders = encoders.ToDictionary(e => e.GetType().Name, e => e);
            _messageTriggers = new List<StateMachine.TriggerWithParameters<Message>>();
            _callbackTriggers = new List<StateMachine.TriggerWithParameters<CallbackQuery>>();
        }

        public void RestoreState(Message message)
        {
            var id = _stateKey.Invoke(message);
            
            if(!Enum.TryParse<State>(_cache.GetString(id),
                out var state)) state = State.Idle;
            
            _machine = new StateMachine(state);
            ConfigureTriggerParameters(_messageTriggers);
            ConfigureStateMachine(_machine);
        }
        
        public void SaveState(Message message)
        {
            var id = _stateKey.Invoke(message);
            _cache.SetString(id, _machine.State.ToString());
        }

        #region Actions

        private static IEnumerable<IEnumerable<T>> Partition<T>(IEnumerable<T> e, int p)
        {
            var enumerator = e.GetEnumerator();
            int i = 0;
            while (enumerator.MoveNext())
            {
                yield return e.Skip(p * i++).Take(p);
            }
        }
        
        private Task SendAlgorithmsKeyboard(Message message)
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

        private async Task SendDocument(CallbackQuery callbackQuery)
        {
            await _client.AnswerCallbackQueryAsync(callbackQuery.Id, Strings.UploadSource);
            await _client.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Strings.UploadSource);

            await _client.SendChatActionAsync(callbackQuery.Message.Chat.Id, ChatAction.UploadPhoto);
            
            const string filePath = @"Files/tux.png";
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fileName = filePath.Split(Path.DirectorySeparatorChar).Last();
            await _client.SendPhotoAsync(callbackQuery.Message.Chat.Id,new InputOnlineFile(fileStream, fileName),"Nice Picture");
        }
        
        #region Usage
        
        private Task Usage(Message message)
        {
            return Usage(message, _doc.Build(_machine.PermittedTriggers));
        }

        private Task Usage(Message message, string doc)
        {
            return _client.SendTextMessageAsync(message.Chat.Id, doc, replyMarkup: new ReplyKeyboardRemove());
        }
        #endregion

        private Task ChooseAlg(CallbackQuery query)
        {
            var callback = JsonSerializer.Deserialize<Callback>(query.Data);
            _logger.LogInformation(query.Data);

            return _client.AnswerCallbackQueryAsync(query.Id);
        }
        #endregion
        
        private void ConfigureStateMachine(StateMachine machine)
        {
            var idle = machine.Configure(State.Idle)
                .PermitReentry(Command.Start)
                .OnEntryFromAsync(MessageTrigger(Command.Start), Usage)
                .Permit(Command.Decode, State.Decode)
                .Permit(Command.Encode, State.Encode);

            var encoding = machine.Configure(State.Encode)
                // .OnEntryFromAsync(CallBackTrigger(Command.Decode), ChooseAlg)
                ;
	
            var decoding = machine.Configure(State.Decode)
                .Permit(Command.Input, State.Source)
                .OnEntryFromAsync(MessageTrigger(Command.Decode), SendAlgorithmsKeyboard);
	
            var source = machine.Configure(State.Source)
                // .OnEntryFromAsync(CallBackTrigger(Command.Input), SendDocument)
                ;
	
            var common = new[] {encoding, source, decoding, idle};
            foreach (var conf in common)
            {
                conf.PermitReentry(Command.Help)
                    .OnEntryFromAsync(MessageTrigger(Command.Help), Usage);
            }
            
            foreach (var conf in common.Take(common.Length - 1))
            {
                conf.Permit(Command.Start, State.Idle);
            }
        }
        
        public async ValueTask OnMessageReceived(
            object sender,
            EventArgs messageEventArgs, 
            CancellationToken token)
        {
            Debug.Assert(messageEventArgs is MessageEventArgs);
            
            var args = (MessageEventArgs) messageEventArgs;
            var message = args.Message;
            
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

            var command = ParseMessage(message.Text);
            var param = MessageTrigger(command);

            using (_logger.BeginScope(scope))
            {
                try
                {
                    await _machine.FireAsync(param, message);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error state firing {Error}", e.Message);
                }
                _logger.LogInformation("Current state {State}", _machine.State);
            }
            SaveState(message);
        }

        private Command ParseMessage(string text)
        {
            var parsed = Enum.TryParse(text
                .Split(' ')
                .First()[1..]
                .Trim(), true, out Command command);

            if (!parsed) return Command.Input;

            return _machine.CanFire(command) 
                ? command 
                : Command.Help;
        }

        private void ConfigureTriggerParameters<T>(
            List<StateMachine.TriggerWithParameters<T>> triggers)
        {
            var commands = Enum.GetValues(typeof(Command)) as Command[];
            triggers.AddRange(commands!.Select(_machine.SetTriggerParameters<T>));
        }

        private StateMachine.TriggerWithParameters<CallbackQuery> CallBackTrigger(params Command[] command)
        {
            return _callbackTriggers.Find(p => command.Any(c => p.Trigger == c));
        }
        
        private StateMachine.TriggerWithParameters<Message> MessageTrigger(params Command[] command)
        {
            return _messageTriggers.Find(p => command.Any(c => p.Trigger == c));
        }
    }
}