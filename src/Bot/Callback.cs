using System;
using Bot.Services;

namespace Bot
{
    [Serializable]
    public struct Callback
    {
        public Guid Id { get; set; }
        public Command Command { get; set; }
    }

}