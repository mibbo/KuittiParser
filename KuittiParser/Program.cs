
using KuittiParser;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;


internal class Program
{
    //private static Dictionary<string, Product> ParseProductsFromReceipt(string path)
    private static List<Product> ParseProductsFromReceipt(string path)
    {
        Dictionary<string, Product> productDictionary = new Dictionary<string, Product>();
        
        using (PdfDocument document = PdfDocument.Open(path))
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
                    if (rowWords.Last().BoundingBox.Left != 192.62890625)
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
        return productDictionary.Select(p => p.Value).ToList();
    }

    private static void Main(string[] args)
    {
        var productDictionary = ParseProductsFromReceipt(@"C:\Users\tommi.mikkola\git\Projektit\KuittiParser\KuittiParser\Kuitit\testikuitti.pdf");

        //var payersDictionary = new Dictionary<string, List<Product>>();
        var payersDictionaryGrouped = new Dictionary<string, Payer>();
        var payersDictionary = new Dictionary<string, Payer>();

        foreach (var product in productDictionary)
        {
            Console.WriteLine(product.Name + " - " + product.Cost);
            Console.WriteLine("Maksajat: ");
            string payer = Console.ReadLine().ToLower().Trim();
            if (string.IsNullOrEmpty(payer))
            {
                payer = "all";
            }

            // Create grouped payer dictionary
            AddProductToPayer(payersDictionaryGrouped, payer, product);


            // if there is multiple payers split 
            if (payer.Contains(','))
            {
                var multiplePayers = payer.Split(',').Select(p => p.Trim()).ToList();
                var divider = multiplePayers.Count;

                var costDivided = product.Cost / divider;

                product.DividedCost = costDivided;      // TODO: Jaa kulut niin, että ei jaa senttejä (eli lisää jollekin sentin jos ei mene tasa)

                foreach (var singlePayer in multiplePayers)
                {
                    AddProductToPayer(payersDictionary, singlePayer, product);
                }
                payersDictionary.Remove(payer);
            }
            else if (payer == "all")
            {
                AddProductToPayer(payersDictionary, payer, product);
            }
            else
            {
                AddProductToPayer(payersDictionary, payer, product);
            }

            //            if (payer.Key.Contains(','))
            //            {
            //                var multiplePayers = payer.Key.Split(',').Select(p => p.Trim()).ToList();
            //                var divider = multiplePayers.Count;

            //                var costDivided = payer.Value / divider;

            //                foreach (var singlePayer in multiplePayers)
            //                {
            //                    AddProductToPayersDict(groupCostsDividedDict, singlePayer, costDivided);
            //                }
            //                groupCostsDividedDict.Remove(payer.Key);
            //            }
            //            else if (payer.Key.Contains("all"))
            //            {
            //                var allPayers = groupCostsDividedDict.Where(p => p.Key != "all").ToList();
            //                var divider = allPayers.Count;
            //                var costDivided = payer.Value / divider;

            //                foreach (var singlePayer in allPayers)
            //                {
            //                    AddProductToPayersDict(groupCostsDividedDict, singlePayer.Key, costDivided);
            //                }
            //                groupCostsDividedDict.Remove(payer.Key);
            //            }
            //AddProductToPayersDict(payersDictionary, payers, product.Value);
        }


        decimal totalCost = 0;
        foreach (var p in payersDictionaryGrouped.OrderByDescending(x => x.Value.GetProductCost()).ToDictionary(x => x.Key, x => x.Value))
        {
            Console.WriteLine($"{p.Key}: {p.Value.GetProductCost()}");
            totalCost += p.Value.GetProductCost();
        }
        Console.WriteLine($"Yhteensä: {totalCost}");

        Console.WriteLine($"");
        Console.WriteLine($"");

        totalCost = 0;
        foreach (var p in payersDictionary.OrderByDescending(x => x.Value.GetPersonalCost()).ToDictionary(x => x.Key, x => x.Value))
        {
            Console.WriteLine($"{p.Key}: {p.Value.GetPersonalCost()}");
            totalCost += p.Value.GetPersonalCost();

            foreach(var product in p.Value.Products)
            {
                Console.WriteLine($" - {product.DividedCost ?? product.Cost}: {product.Name}");
            }
        }
        Console.WriteLine($"Yhteensä: {totalCost}");
    }


    private static void AddProductToPayer(Dictionary<string, Payer> payersDict, string payer, Product product)
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

    //        Console.WriteLine("Maksajat ryhmittäin:");
    //        decimal totalCost = 0;
    //        foreach (var payer in payersDictionary.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value))
    //        {
    //            Console.WriteLine($"{payer.Key}: {payer.Value}");
    //            totalCost = totalCost + payer.Value;
    //        }
    //        Console.WriteLine($"Yhteensä: {totalCost}");








    //        Console.WriteLine("");
    //        totalCost = 0;
    //        Console.WriteLine("Maksajat yksittäin:");
    //        var payersCostsDivided = DivideGroupCosts(payersDictionary);
    //        foreach (var payer in payersCostsDivided.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value))
    //        {
    //            Console.WriteLine($"{payer.Key}: {payer.Value}");
    //            totalCost = totalCost + payer.Value;
    //        }
    //        Console.WriteLine($"Yhteensä: {totalCost}");
    //    }

    //    // Jakaa ryhmittäiset kustannukset tommi,mauri 10,00 => tommi 5,00 ja mauri 5,00)
    //    // palauttaa uuden jaotellun dictin
    //    private static Dictionary<string, decimal> DivideGroupCosts(Dictionary<string, decimal> payersDict)
    //    {
    //        var groupCostsDividedDict = payersDict;

    //        foreach (var payer in payersDict.ToList())
    //        {
    //            if (payer.Key.Contains(','))
    //            {
    //                var multiplePayers = payer.Key.Split(',').Select(p => p.Trim()).ToList();
    //                var divider = multiplePayers.Count;

    //                var costDivided = payer.Value / divider;

    //                foreach (var singlePayer in multiplePayers)
    //                {
    //                    AddProductToPayersDict(groupCostsDividedDict, singlePayer, costDivided);
    //                }
    //                groupCostsDividedDict.Remove(payer.Key);
    //            }
    //            else if (payer.Key.Contains("all"))
    //            {
    //                var allPayers = groupCostsDividedDict.Where(p => p.Key != "all").ToList();
    //                var divider = allPayers.Count;
    //                var costDivided = payer.Value / divider;

    //                foreach (var singlePayer in allPayers)
    //                {
    //                    AddProductToPayersDict(groupCostsDividedDict, singlePayer.Key, costDivided);
    //                }
    //                groupCostsDividedDict.Remove(payer.Key);
    //            }
    //        }
    //        return groupCostsDividedDict;
    //    }

    //    private static void AddProductToPayersDict(Dictionary<string, List<Product>> payersDict, string payers, Product product)
    //    {

    //        if (payersDict.ContainsKey(payers))
    //        {
    //            var newAllCost = payersDict[payers] + product;
    //            payersDict[payers] = newAllCost;
    //        }
    //        else
    //        {
    //            payersDict.Add(payers, product);
    //        }
    //}
}