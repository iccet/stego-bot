using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bot.Services;
using Telegram.Bot.Types;

namespace Bot.Interfaces
{
    public interface IDocBuilder
    {
        string Build(IEnumerable<Command> triggers);
    }
}