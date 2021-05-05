using System;
using System.Runtime.Serialization;

namespace Bot.Services
{
    [Serializable]
    public enum Command
    {
        Start,
        Inline,
        Encode,
        Decode,
        Help,
        Input
    }
}