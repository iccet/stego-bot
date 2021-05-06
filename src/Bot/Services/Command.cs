using System;
using System.Runtime.Serialization;

namespace Bot.Services
{
    [Serializable]
    public enum Command
    {
        Start,
        Encode,
        Decode,
        Help,
        Input
    }
}