using KuittiBot.Functions.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace KuittiBot.Functions.Domain.Abstractions
{
    public interface IBotStateMachine
    {
        Task OnUpdate(Update update);
    }
}
