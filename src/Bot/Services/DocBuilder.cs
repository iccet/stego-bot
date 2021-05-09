using System.Collections.Generic;
using System.Linq;
using Bot.Interfaces;

namespace Bot.Services
{
    public class DocBuilder : IDocBuilder
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
                {Command.ChooseAlg, () => Commands.Input},
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