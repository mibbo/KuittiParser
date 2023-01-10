using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KuittiParser
{
    internal class ProductModel
    {
        public string Id{ get; set; }
        public string Name { get; set; }
        public HashSet<string> Payers { get; set; }
        private decimal costField;
        public decimal Cost
        {
            get
            {
                return this.costField;
            }
            set
            {
                this.costField += value;
            }
        }

    }


}
