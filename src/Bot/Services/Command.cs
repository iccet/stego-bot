using System;

namespace Bot.Services
{
    [Serializable]
    public enum Command
    {
        start,
        inline,
        sub,
        unsub,
        help,
        input
    }
}