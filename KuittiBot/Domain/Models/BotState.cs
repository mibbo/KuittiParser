﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KuittiBot.Functions.Domain.Models
{
    public enum BotState
    {
        WaitingForInput,
        ReceivedReceipt,
        ReceivedPayers,
        AskingParticipants,
        AllocatingItems,
        Summary
    }
}
