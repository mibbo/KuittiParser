using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KuittiBot.Functions.Domain.Models
{
    public class Receipt
    {
        public string Name { get; set; }
        public string ShopName { get; set; }
        public decimal RawTotalCost { get; set; }
        public float Confidence { get; set; }
        public List<Product> Products { get; set; }
        public List<Payer> Payers { get; set; }
        public decimal GetReceiptTotalCost()
        {
            decimal totalCost = 0;
            foreach (var product in Products)
            {
                totalCost += product.Cost;
            }
            return decimal.Round(totalCost, 2, MidpointRounding.AwayFromZero);
        }
    }

    public class Payer
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
            return decimal.Round(totalCost, 2, MidpointRounding.AwayFromZero);
        }
        public decimal GetPersonalCost()
        {
            decimal totalCost = 0;
            foreach (var product in Products)
            {
                totalCost += product.DividedCost ?? product.Cost;
            }
            return decimal.Round(totalCost, 2, MidpointRounding.AwayFromZero);
        }
    }

    public class Product
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public decimal? DividedCost { get; set; }
        public List<decimal>? Discounts { get; set; }
        private decimal costField;
        public decimal Cost
        {
            get
            {
                var costAfterDiscounts = this.costField;
                if (Discounts != null)
                {
                    foreach (var discount in Discounts)
                    {
                        costAfterDiscounts += discount;
                    }
                }

                return costAfterDiscounts;
            }
            set
            {
                this.costField += value;
            }
        }

    }


}
