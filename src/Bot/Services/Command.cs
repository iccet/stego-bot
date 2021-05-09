using System;

namespace Bot.Services
{
    [Serializable]
    public enum Command
    {
        Start,
        Encode,
        Decode,
        Help,
        AlgList,
        SetAlg,
        UploadSource
    }
}