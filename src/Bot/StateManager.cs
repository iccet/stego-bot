using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bot.Interfaces;
using Bot.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Bot
{
    using StateMachine = Stateless.StateMachine<State, Command>;
    
    using MessageTrigger = Stateless.StateMachine<State, Command>
        .TriggerWithParameters<Message>;

    using CallbackTrigger = Stateless.StateMachine<State, Command>
        .TriggerWithParameters<CallbackQuery>;

    public class StateManager : IStateManager
    {
        private delegate string StateKey(Message message);
        private readonly StateKey _stateKey = message => message.Chat.Id.ToString();
        
        private readonly IDistributedCache _cache;
        private readonly ILogger<StateManager> _logger;
        private readonly List<MessageTrigger> _messageTriggers;
        private readonly List<CallbackTrigger> _callbackTriggers;
        private readonly IDocBuilder _doc;

        private StateMachine _machine;
        private WorkflowState _state;
        private readonly IWorkflow _workflow;

        public StateManager(
            IDistributedCache cache,
            ILogger<StateManager> logger,
            IWorkflow workflow, 
            IDocBuilder doc)
        {
            _cache = cache;
            _logger = logger;
            _workflow = workflow;
            _doc = doc;
            _messageTriggers = new List<StateMachine.TriggerWithParameters<Message>>();
            _callbackTriggers = new List<StateMachine.TriggerWithParameters<CallbackQuery>>();
        }

        public void RestoreState(Message message)
        {
            var id = _stateKey.Invoke(message);
            var cached = _cache.GetString(id);
            try
            {
                _state = JsonSerializer.Deserialize<WorkflowState>(cached);
            }
            catch (Exception e) when(e is JsonException || e is ArgumentNullException)
            {
                _state = new WorkflowState();
            }
            
            _machine = new StateMachine(_state.State);
            ConfigureStateMachine(_machine);
        }
        
        public void SaveState(Message message)
        {
            var id = _stateKey.Invoke(message);
            _state.State = _machine.State;
            _cache.SetString(id, JsonSerializer.Serialize(_state));
        }

        public async ValueTask OnCallbackQueryReceived(
            object sender,
            EventArgs callbackQueryEventArgs,
            CancellationToken token)
        {
            Debug.Assert(_machine != null);
            Debug.Assert(callbackQueryEventArgs is CallbackQueryEventArgs);
            
            var args = (CallbackQueryEventArgs) callbackQueryEventArgs;
            var query = args.CallbackQuery;
            
            try
            {
                var param = CallBackTrigger(Command.ChooseAlg);
                
                _state.Callback = JsonSerializer.Deserialize<Callback>(query.Data);
                await _machine.FireAsync(param, query);
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
            }
            SaveState(query.Message);
        }
        
        public async ValueTask OnMessageReceived(
            object sender,
            EventArgs messageEventArgs, 
            CancellationToken token)
        {
            Debug.Assert(_machine != null);
            Debug.Assert(messageEventArgs is MessageEventArgs);
            
            var args = (MessageEventArgs) messageEventArgs;
            var message = args.Message;
            
            var scope = new Dictionary<string, object>
            {
                {"UserId", message.From.Username},
                {"Event", nameof(OnMessageReceived)}
            };

            var command = ParseMessage(message);
            
            var param = MessageTrigger(command);

            using (_logger.BeginScope(scope))
            {
                try
                {
                    await _machine.FireAsync(param, message);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error state firing {Trigger}", param.Trigger);
                }
                _logger.LogInformation("Current state {State}", _machine.State);
            }
            SaveState(message);
        }

        private void SetLocale(Message message)
        {
            var locale = CultureInfo.GetCultureInfo(
#if  DEBUG
            "ru"      
#else
            message.From.LanguageCode
#endif
            );
            
            Commands.Culture = locale;
            Strings.Culture = locale;
            
            _logger.LogInformation("Set locale to {Locale}", locale.Name);
        }

        private void SetLocale(CallbackQuery query)
        {
            SetLocale(query.Message);
        }

        private Command ParseMessage(Message message)
        {
            if (message.Type == MessageType.Photo) 
                return Command.UploadSource;
            
            var parsed = Enum.TryParse(message.Text
                .Split(' ')
                .First()[1..]
                .Trim(), true, out Command command);

            if (!parsed) return Command.ChooseAlg;

            return _machine.CanFire(command) 
                ? command 
                : Command.Help;
        }

        private void ConfigureTriggerParameters<T>(
            List<StateMachine.TriggerWithParameters<T>> triggers, params Command[] command)
        {
            triggers.AddRange(command!.Select(_machine.SetTriggerParameters<T>));
        }

        private Task Usage(Message message)
        {
            return _workflow.Usage(message, _doc.Build(_machine.PermittedTriggers));
        }
        
        private void ConfigureStateMachine(StateMachine machine)
        {
            ConfigureTriggerParameters(_messageTriggers, 
                Command.Help,
                Command.Start,
                Command.UploadSource,
                Command.Decode);
            
            ConfigureTriggerParameters(_callbackTriggers,
                Command.ChooseAlg);
            
            var idle = machine.Configure(State.Idle)
                .PermitReentry(Command.Start)
                .OnEntryFromAsync(MessageTrigger(Command.Start), Usage)
                .OnEntryFromAsync(MessageTrigger(Command.UploadSource), _workflow.DecodeSource)
                .Permit(Command.Decode, State.Decode)
                .Permit(Command.Encode, State.Encode);

            var encoding = machine.Configure(State.Encode)
                // .OnEntryFromAsync(CallBackTrigger(Command.Decode), ChooseAlg)
                ;
	
            var decoding = machine.Configure(State.Decode)
                .PermitReentry(Command.ChooseAlg)
                .Permit(Command.UploadSource, State.Idle)
                .OnEntryFromAsync(CallBackTrigger(Command.ChooseAlg), _workflow.RequestSource)
                .OnEntryFromAsync(MessageTrigger(Command.Decode), _workflow.SendAlgorithmsList);
	
            var common = new[] {encoding, decoding, idle};
            foreach (var conf in common)
            {
                conf.PermitReentry(Command.Help)
                    .OnEntryFrom(MessageTrigger(Command.Help), SetLocale)
                    .OnEntryFromAsync(MessageTrigger(Command.Help), Usage);
            }
            
            foreach (var conf in common.Take(common.Length - 1))
            {
                conf.Permit(Command.Start, State.Idle);
            }
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