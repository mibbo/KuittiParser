using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace KuittiBot.Functions.Domain.Models
{
    public class SessionInfo
    {
        public string UserId { get; set; }
        public string Hash { get; set; }
        public string FileName { get; set; }
        public string DocumentType { get; set; }
        public bool SessionSuccessful { get; set; }
        public string ShopName { get; set; }
        public decimal RawTotalCost { get; set; }
        public bool GroupMode { get; set; }
    }
}
