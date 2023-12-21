using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KuittiBot.Functions.Domain.Models
{
    public class UserDataEntity : TableEntity
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public BotState CurrentState { get; set; }
    }
}
