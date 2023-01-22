﻿using Microsoft.Azure.Cosmos.Table;
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
        public string FileName { get; set; }
        public string FileId { get; set; }
        public string UserName { get; set; }
        public string Description { get; set; }
        public string PayersRaw { get; set; }
        public bool ProcessEnd { get; set; }
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
