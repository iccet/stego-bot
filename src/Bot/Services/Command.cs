using System;
using System.ComponentModel;

namespace Bot.Services
{
    [Serializable]
    public enum Command
    {
        [Description("Начать работу с ботом со сброшенным состоянием")]
        start,
        
        [Description("Авторизоваться на SpeachflowBot")]
        login,
        
        [Description("Деавторизоваться на SpeachflowBot")]
        logout,

        [Description("Войти в inline режим")]
        inline,

        [Description("Подписаться на оповещения")]
        sub,
        
        [Description("Отписаться от оповещений")]
        unsub,

        [Description("Показать помощь")]
        help,
        
        input
    }
}