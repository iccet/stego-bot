using System;

namespace Bot.Services
{
    [Serializable]
    public enum State
    {
        Idle,
        Inline,
        Source,
        Alg,
        Data,
    }
}