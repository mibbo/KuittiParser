using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KuittiParser
{
    internal class Receipt
    {
        public string Name { get; set; }
        public string Description { get; set; }

    }

    internal class Payer
    {
        public string Name { get; set; }
        public List<Product>? Products { get; set; }
        public decimal GetProductCost()
        {
            decimal totalCost = 0;
            foreach (var product in Products)
            {
                totalCost += product.Cost;
            }
            return totalCost;
        }
        public decimal GetPersonalCost()
        {
            decimal totalCost = 0;
            foreach(var product in Products)
            {
                totalCost += product.DividedCost ?? product.Cost;
            }
            return totalCost;
        }
    }

    internal class Product
    {
        public string Id{ get; set; }
        public string Name { get; set; }
        public decimal? DividedCost { get; set; }
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
