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
        private decimal costField;
        public decimal Cost
        {
            get
            {
                return this.costField;
            }
            set
            {
                //if (this.costField < 0)
                //{
                //    this.costField = this.costField - value;

                //}
                this.costField += value;
            }
        }

    }


}
