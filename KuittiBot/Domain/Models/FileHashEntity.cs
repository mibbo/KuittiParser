using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KuittiBot.Functions.Domain.Models
{
    public class FileHashEntity : TableEntity
    {
        public string FileName { get; set; }
        public string Hash { get; set; }
    }
}
