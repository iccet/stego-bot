using System;
using System.Collections.Generic;

namespace Bot.Services
{
    [Serializable]
    public enum State
    {
        Idle,
        Encode,
        Decode,
    }

    public struct WorkflowState
    {
        public State State { get; set; }
        public Callback Callback { get; set; }
    }
}