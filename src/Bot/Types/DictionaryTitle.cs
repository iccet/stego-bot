using System;

namespace Bot.Types
{
    public class DictionaryTitle : BaseEntity<Guid>
    {
        public DictionaryType Type { get; set; }
    }
}