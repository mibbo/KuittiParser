using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KuittiBot.Functions.Domain.Models
{
    public class UserDataCacheEntity : TableEntity
    {
        public string Id { get; set; }
        //public string FileName { get; set; }                  // pitäisikö tehdä tiedostokohtainen state vai user kohtainen?
        public string UserName { get; set; }
        public BotState CurrentState { get; set; }
        //[IgnoreProperty]
        //public List<string> Payers
        //{
        //    get
        //    {
        //        return JsonConvert.DeserializeObject<List<string>>(PayersRaw);
        //    }

        //    set
        //    {
        //        PayersRaw = JsonConvert.SerializeObject(value);
        //    }
        //}
    }
}
