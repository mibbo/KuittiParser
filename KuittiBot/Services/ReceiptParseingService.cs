using System;
using System.Data;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig;
using KuittiBot.Functions.Domain.Models;

namespace KuittiBot.Functions.Services
{
    public class ReceiptParseingService
    {
	    public ReceiptParseingService()
	    {
        }

        public static Receipt ParseProductsFromReceipt(Stream stream)
        {
            Dictionary<string, Product> productDictionary = new Dictionary<string, Product>();
            var receipt = new Receipt();
            // TODO: Add receipt metadata while parsing it (Shop name, 

            using (PdfDocument document = PdfDocument.Open(stream))
            {
                foreach (Page page in document.GetPages())
                {
                    var wordList = page.GetWords().ToList();

                    // Create Lists of words for each receipt row number
                    List<List<Word>> rowList = wordList.GroupBy(it => it.BoundingBox.Bottom).Select(grp => grp.ToList()).ToList();
                    //Dictionary<double, List<Word>> orderDictionary = wordList.GroupBy(it => it.BoundingBox.Bottom).ToDictionary(dict => dict.Key, dict => dict.Select(item => item).ToList());

                    var previousProduct = new Product();

                    // Dictionary for products to be added

                    // Loop rows
                    foreach (var rowWords in rowList.Skip(4))
                    {
                        var words = rowWords.Select(word => word.Text).ToList();

                        if (words.First() == "YHTEENSÄ")
                        {
                            // TODO: looppaa ja laske yhteen että mätsääkö YHTEENSÄ ja käsiteltyjen rivien summat
                            break;
                        }

                        // Skip rows that are not product rows
                        if (rowWords.Last().Text == "----------")
                            continue;
                        if (rowWords.Last().Letters.Where(l => l.Value != "-").ToList().Last().StartBaseLine.X != 207.03125)
                            continue;

                        var currentRowCost = words.Last();

                        if (words.Last().Contains('-'))
                        {
                            Regex rgx = new("[^a-zA-Z0-9 ,]");
                            currentRowCost = rgx.Replace(currentRowCost, "");
                            var negatedCost = decimal.Parse(currentRowCost) * -1;
                            productDictionary[previousProduct.Id].Cost = negatedCost;
                            continue;
                        }

                        if (words.FirstOrDefault() == "PANTTI" && !currentRowCost.Contains('-'))
                        {
                            productDictionary[previousProduct.Id].Cost = decimal.Parse(currentRowCost);
                            continue;
                        }

                        var product = new Product
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = string.Join(" ", words.SkipLast(1)),
                            Cost = decimal.Parse(words.Last())
                        };
                        productDictionary.Add(product.Id, product);

                        previousProduct = product;
                    }
                }
            }
            receipt.Products = productDictionary.Select(p => p.Value).ToList();
            return receipt;
        }

        private static void PrintReceipt(Dictionary<string, Payer> payersDict, bool printExtended = false)
        {
            Console.WriteLine($"");
            Console.WriteLine(printExtended ? "Yksittäiset kustannukset:" : "Ryhmittäiset kustannukset:");
            decimal totalCost = 0;
            foreach (var p in payersDict.OrderByDescending(x => (printExtended ? x.Value.GetPersonalCost() : x.Value.GetProductCost())).ToDictionary(x => x.Key, x => x.Value))
            {
                Console.WriteLine($"{p.Key}: {(printExtended ? p.Value.GetPersonalCost() : p.Value.GetProductCost())}");
                totalCost += (printExtended ? p.Value.GetPersonalCost() : p.Value.GetProductCost());

                if (printExtended)
                {
                    foreach (var product in p.Value.Products)
                    {
                        var printableCost = product.DividedCost ?? product.Cost;
                        Console.WriteLine($" - {decimal.Round(printableCost, 2, MidpointRounding.AwayFromZero)}: {product.Name}");  //printableCost:0.00
                    }
                }
            }
            Console.WriteLine($"Yhteensä: {totalCost}");
            Console.WriteLine($"");
        }

        private static void AddProductToPayer(Dictionary<string, Payer> payersDict, string payer, Product product, Receipt receipt)
        {
            if (payersDict.ContainsKey(payer))
            {
                payersDict[payer].Products.Add(product);
            }
            else
            {
                var newProduct = product;
                var newPayer = new Payer
                {
                    Name = payer,
                    Products = new List<Product> { newProduct }
                };
                payersDict.Add(payer, newPayer);
            }
        }
    }
}