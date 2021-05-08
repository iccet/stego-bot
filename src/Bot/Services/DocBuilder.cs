using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using StateMachine = Stateless.StateMachine<Bot.Services.State, Bot.Services.Command>;

namespace Bot.Services
{
    public class DocBuilder
    {
        private delegate string DocString();
        private readonly Dictionary<Command, DocString> _commandDocs;
        private const string CommandDocFormat = "/{0} : {1}";

        public DocBuilder()
        {
            _commandDocs = new Dictionary<Command, DocString>
            {
                {Command.Help, () => Commands.Help},
                {Command.Start, () => Commands.Start},
                {Command.Encode, () => Commands.Encode},
                {Command.Input, () => Commands.Input},
                {Command.Decode, () => Commands.Decode},
            };
        }

        public string Build(IEnumerable<Command> triggers)
        {
            return string.Join('\n', triggers.Select(t => string.Format(
                CommandDocFormat, 
                t.ToString("G").ToLower(),
                _commandDocs[t]())));
        }

    }
}