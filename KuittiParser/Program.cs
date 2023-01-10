
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
    private static Dictionary<string, ProductModel> ParseProductsFromReceipt(string path)
    {
        Dictionary<string, ProductModel> productDictionary = new Dictionary<string, ProductModel>();
        
        using (PdfDocument document = PdfDocument.Open(path))
        {
            foreach (Page page in document.GetPages())
            {
                var wordList = page.GetWords().ToList();

                // Create Lists of words for each receipt row number
                List<List<Word>> rowList = wordList.GroupBy(it => it.BoundingBox.Bottom).Select(grp => grp.ToList()).ToList();
                //Dictionary<double, List<Word>> orderDictionary = wordList.GroupBy(it => it.BoundingBox.Bottom).ToDictionary(dict => dict.Key, dict => dict.Select(item => item).ToList());

                var previousProduct = new ProductModel();

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

                    var product = new ProductModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = string.Join(" ", words.SkipLast(1)),
                        Cost = decimal.Parse(words.Last()),
                        Payers = new HashSet<string>()
                    };
                    productDictionary.Add(product.Id, product);

                    previousProduct = product;
                }
            }
        }
        return productDictionary;
    }

    private static void Main(string[] args)
    {
        var productDictionary = ParseProductsFromReceipt(@"C:\Users\tommi.mikkola\git\Projektit\KuittiParser\KuittiParser\Kuitit\testikuitti.pdf");

        var payersDictionary = new Dictionary<string, decimal>();

        foreach (var product in productDictionary)
        {
            Console.WriteLine(product.Value.Name + " - " + product.Value.Cost);
            Console.WriteLine("Maksajat: ");
            string payers = Console.ReadLine().ToLower();
            if (string.IsNullOrEmpty(payers))
            {
                payers = "all";
            }
            productDictionary[product.Value.Id].Payers.Add(payers);
            AddCostToPayersDict(payersDictionary, payers, product.Value.Cost);
        }

        Console.WriteLine("Maksajat ryhmittäin:");
        decimal totalCost = 0;
        foreach (var payer in payersDictionary.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value))
        {
            Console.WriteLine($"{payer.Key}: {payer.Value}");
            totalCost = totalCost + payer.Value;
        }
        Console.WriteLine($"Yhteensä: {totalCost}");
        Console.WriteLine("");
        totalCost = 0;
        Console.WriteLine("Maksajat yksittäin:");
        var payersCostsDivided = DivideGroupCosts(payersDictionary);
        foreach (var payer in payersCostsDivided.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value))
        {
            Console.WriteLine($"{payer.Key}: {payer.Value}");
            totalCost = totalCost + payer.Value;
        }
        Console.WriteLine($"Yhteensä: {totalCost}");
    }

    // Jakaa ryhmittäiset kustannukset tommi,mauri 10,00 => tommi 5,00 ja mauri 5,00)
    // palauttaa uuden jaotellun dictin
    private static Dictionary<string, decimal> DivideGroupCosts(Dictionary<string, decimal> payersDict)
    {
        var groupCostsDividedDict = payersDict;

        foreach (var payer in payersDict.ToList())
        {
            if (payer.Key.Contains(','))
            {
                var multiplePayers = payer.Key.Split(',').Select(p => p.Trim()).ToList();
                var divider = multiplePayers.Count;

                var costDivided = payer.Value / divider;

                foreach (var singlePayer in multiplePayers)
                {
                    AddCostToPayersDict(groupCostsDividedDict, singlePayer, costDivided);
                }
                groupCostsDividedDict.Remove(payer.Key);
            }
            else if (payer.Key.Contains("all"))
            {
                var allPayers = groupCostsDividedDict.Where(p => p.Key != "all").ToList();
                var divider = allPayers.Count;
                var costDivided = payer.Value / divider;

                foreach (var singlePayer in allPayers)
                {
                    AddCostToPayersDict(groupCostsDividedDict, singlePayer.Key, costDivided);
                }
                groupCostsDividedDict.Remove(payer.Key);
            }
        }
        return groupCostsDividedDict;
    }

    private static void AddCostToPayersDict(Dictionary<string, decimal> payersDict, string payers, decimal productCost)
    {
        
        if (payersDict.ContainsKey(payers))
        {
            var newAllCost = payersDict[payers] + productCost;
            payersDict[payers] = newAllCost;
        }
        else
        {
            payersDict.Add(payers, productCost);
        }
    }
}