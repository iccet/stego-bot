using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Bot.Interfaces;

namespace Bot.Services
{
    using StateMachine = Stateless.StateMachine<State, Command>;
    public class DocBuilder : IDocBuilder
    {
        private delegate string DocString();
        private readonly Dictionary<Command, DocString> _commandDocs;
        private readonly Dictionary<State, DocString> _stateDocs;
        private const string CommandDocFormat = "/{0} : {1}";

        public DocBuilder()
        {
            _commandDocs = new Dictionary<Command, DocString>
            {
                {Command.Help, () => Commands.Help},
                {Command.Start, () => Commands.Start},
                {Command.Encode, () => Commands.Encode},
                {Command.AlgList, () => Commands.Alg},
                {Command.Decode, () => Commands.Decode},
            };
            _stateDocs = new Dictionary<State, DocString>
            {
                {State.Idle, () => States.Idle},
                {State.Decode, () => States.Decode},
                {State.Encode, () => States.Idle},
            };
        }

        public string Build(StateMachine machine)
        {
            var state = _stateDocs[machine.State]();
            var commands = string.Join('\n', machine.PermittedTriggers.Where(t => _commandDocs.ContainsKey(t))
            .Select(t => string.Format(CommandDocFormat, 
                t.ToString("G").ToLower(),
                _commandDocs[t]())));
            
            return string.Join('\n', state, commands);
        }
    }
}