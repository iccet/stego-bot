using System;

namespace Bot.Services
{
    [Serializable]
    public enum State
    {
        Idle,
        Encode,
        Decode,
        Source,
    }
}