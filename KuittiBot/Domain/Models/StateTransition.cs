using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace KuittiBot.Functions.Domain.Models
{
    internal class StateTransition
    {
        public BotState CurrentState { get; set; }
        public BotState NextState { get; set; }
        public BotEvent Event { get; set; }
        public Func<Update, Task> Action { get; set; }
    }
}
