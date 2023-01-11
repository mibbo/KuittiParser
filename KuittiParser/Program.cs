﻿
using KuittiParser;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;


internal class Program
{
    //TODO:
    //- Lopuks input että onko muita osallistujia, jotka osallistuu vain "all" kustannuksiin
    //- printtaa aina kappalemäärän jos ostettu useampi
    //	-> halutaanko jakaa osiin?

    //private static Dictionary<string, Product> ParseProductsFromReceipt(string path)
    private static Receipt ParseProductsFromReceipt(string path)
    {
        Dictionary<string, Product> productDictionary = new Dictionary<string, Product>();
        var receipt = new Receipt();
        // TODO: Add receipt metadata while parsing it (Shop name, 

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
                    if (rowWords.Last().Letters.Last().StartBaseLine.X != 207.03125)
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

    private static void Main(string[] args)
    {
        Receipt receipt = ParseProductsFromReceipt(@"C:\Users\tommi.mikkola\git\Projektit\KuittiParser\KuittiParser\Kuitit\maukan_kuitti.pdf");
        var groupedReceipt = receipt;
        var payersDictionaryGrouped = new Dictionary<string, Payer>();
        var payersDictionary = new Dictionary<string, Payer>();

        foreach (var product in receipt.Products)
        {
            Console.WriteLine(product.Name + " - " + product.Cost);
            Console.WriteLine("Maksajat: ");
            string payer = Console.ReadLine().ToLower().Trim();

           // TODO: var validity = CheckInputValidity(payer); Palautta mahdollinen virhe käyttäjälle

            if (string.IsNullOrEmpty(payer))
            {
                payer = "all";
            }

            // Create grouped payer dictionary
            AddProductToPayer(payersDictionaryGrouped, payer, product, groupedReceipt);


            // Split expenses to multiple payers
            if (payer.Contains(','))
            {
                var multiplePayers = payer.Split(',').Select(p => p.Trim()).ToList();
                var divider = multiplePayers.Count;

                var costDivided = product.Cost / divider;

                product.DividedCost = costDivided;      // TODO: Jaa kulut niin, että ei jaa senttejä (eli lisää jollekin sentin jos ei mene tasa)

                foreach (var singlePayer in multiplePayers)
                {
                    AddProductToPayer(payersDictionary, singlePayer, product, receipt);
                }
                payersDictionary.Remove(payer);
            }
            else if (payer == "all")
            {
                AddProductToPayer(payersDictionary, payer, product, receipt);
            }
            else
            {
                AddProductToPayer(payersDictionary, payer, product, receipt);
            }
        }


        // Split expenses that belong to every payer
        if (payersDictionary.ContainsKey("all"))
        {
            Console.WriteLine("Onko muita maksajia yhteisiin ostoksiin?");
            var additionalPayers = Console.ReadLine().Trim().Split(',').ToList();
            if (additionalPayers.Count > 0)
            {
                foreach (var payer in additionalPayers)
                {
                    payersDictionary.Add(payer, new Payer { Name = payer, Products = new List<Product>() });
                }

                receipt.Payers = payersDictionary.Where(l => l.Key != "all").Select(payer => payer.Value).ToList();
                foreach (var product in payersDictionary["all"].Products)
                {
                    var divider = receipt.Payers.Count;

                    foreach (var payer in receipt.Payers)
                    {
                        var costDivided = product.Cost / divider;
                        product.DividedCost = costDivided;
                        AddProductToPayer(payersDictionary, payer.Name, product, receipt);
                    }
                }
                var payersAmount = receipt.Payers.Count;
                payersDictionary.Remove("all");
            }

        }




        // Print end results
        Console.WriteLine($"");
        PrintReceipt(payersDictionaryGrouped);
        PrintReceipt(payersDictionary, true);

        foreach(var payer in receipt.Payers)
        {
            Console.WriteLine(payer.Name);
        }
    }

    private static void PrintReceipt(Dictionary<string, Payer> payersDict, bool printExtended = false)
    {
        Console.WriteLine($"");
        Console.WriteLine(printExtended ? "Yksittäiset kustannukset:" : "Ryhmittäiset kustannukset:");
        decimal totalCost = 0;
        foreach (var p in payersDict.OrderByDescending(x => (printExtended ? x.Value.GetPersonalCost() : x.Value.GetProductCost())).ToDictionary(x => x.Key, x => x.Value))
        {
            Console.WriteLine($"{p.Key}: {(printExtended? p.Value.GetPersonalCost() : p.Value.GetProductCost())}");
            totalCost += (printExtended ? p.Value.GetPersonalCost() : p.Value.GetProductCost());

            if (printExtended)
            {
                foreach (var product in p.Value.Products)
                {
                    Console.WriteLine($" - {product.DividedCost ?? product.Cost}: {product.Name}");
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