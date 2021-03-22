using System;

namespace Bot.Services
{
    [Serializable]
    public enum State
    {
        idle,
        inline,
        encoding,
    }
}