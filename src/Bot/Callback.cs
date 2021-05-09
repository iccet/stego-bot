using System;
using Bot.Services;

namespace Bot
{
    [Serializable]
    public struct Callback
    {
        public Command Command { get; set; }
        public string Alg { get; set; }
    }

}