using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KuittiBot.Functions.Domain.Models
{
    public class UserSessionInfo
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string FileName { get; set; }
        public string Hash { get; set; }
        public string FileId { get; set; }
        public string DocumentType { get; set; }
        public string Confidence { get; set; }
    }
}
