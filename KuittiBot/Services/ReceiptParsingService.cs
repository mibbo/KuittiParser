﻿using System;
using System.Data;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig;
using KuittiBot.Functions.Domain.Models;
using System.IO;
using System.Globalization;
using KuittiBot.Functions.Domain.Abstractions;

namespace KuittiBot.Functions.Services
{
    public class ReceiptParsingService : IReceiptParsingService
    {
	    public ReceiptParsingService()
	    {
        }

        public Receipt ParseProductsFromReceipt(Stream stream)
        {
            List<string> supportedShops = new List<string> { "K-Market", "Prisma", "S-market", "Sale", "Sokos" };

            Dictionary<string, double> shopReceiptCoordinates = new Dictionary<string, double>
            { 
                { "K-Market", 441.57807548136384},
                { "Prisma",   211.83203125},
                { "S-market", 211.83203125},
                { "Sale",     211.83203125},
                { "Sokos",    211.83203125}
            };

            Dictionary<string, Product> productDictionary = new Dictionary<string, Product>();
            var receipt = new Receipt();
            // TODO: Add receipt metadata while parsing it (Shop name, 

            var path = @"C:\Users\tommi.mikkola\git\Projektit\KuittiParser\KuittiParses.Console\Kuitit\testikuitti_prisma.pdf";

            using (PdfDocument document = PdfDocument.Open(stream))
            //using (PdfDocument document = PdfDocument.Open(path))
            {
                foreach (Page page in document.GetPages())
                {
                    List<List<Word>> rowList = new();

                    var wordList = page.GetWords().ToList();

                    var asd = wordList.FirstOrDefault(w => supportedShops.Any(shop => w.Text.ToLower().Contains(shop)))?.Text ?? " ";

                    var shopName = wordList
                        .SelectMany(w => supportedShops.Where(shop => w.Text.ToLower().Contains(shop.ToLower())).Select(shop => shop))
                        .FirstOrDefault();

                    receipt.ShopName = shopName;

                    try
                    {
                        // Create Lists of words for each receipt row number
                        rowList = wordList.GroupBy(it => it.BoundingBox.Bottom).Select(grp => grp.ToList()).ToList();
                        //Dictionary<double, List<Word>> orderDictionary = wordList.GroupBy(it => it.BoundingBox.Bottom).ToDictionary(dict => dict.Key, dict => dict.Select(item => item).ToList());


                        // Locate first product row: Checks the coordinate and that the cost-word contains comma
                        // This has a problem if the coordinate is not the same in all receipts.
                        // Possible fix: Check all rows until "YHTEENSÄ" and keep those that have comma in the last word (alleged cost-word)
                        var firstProductRow = rowList.Where(x => x.LastOrDefault().Letters.LastOrDefault().EndBaseLine.X == shopReceiptCoordinates[receipt.ShopName] && x.LastOrDefault().Text.Contains(',')).FirstOrDefault();

                        var index = rowList.IndexOf(firstProductRow) - 1;
                        // Remove all rows before first product row product rows
                        rowList.RemoveRange(0, index + 1);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error: Something went wrong during the initial parsing of the pdf file. This kind of receipt is not supported yet.", ex);
                    }


                    // Loop product rows
                    var previousProduct = new Product();
                    foreach (var rowWords in rowList)
                    {
                        try
                        {

                            var words = rowWords.Select(word => word.Text).ToList();

                            if (words.First() == "YHTEENSÄ")
                            {
                                receipt.Products = productDictionary.Select(p => p.Value).ToList();

                                receipt.RawTotalCost = decimal.Parse(words.LastOrDefault(), new CultureInfo("fi", true));

                                var calculatedTotalCost = receipt.GetReceiptTotalCost();
                                if (calculatedTotalCost != receipt.RawTotalCost)
                                {
                                    throw new Exception($"Error: Something went wrong during the product parsing. Program calculated total cost is '{calculatedTotalCost}' when the actual cost in the receipt is '{receipt.RawTotalCost}'");
                                }

                                break;
                            }

                            // Skip rows that are not product rows
                            if (rowWords.Last().Text.Contains("------"))
                                continue;
                            if (rowWords.Last().Letters.Where(l => l.Value != "-").ToList().LastOrDefault().EndBaseLine.X != shopReceiptCoordinates[receipt.ShopName])
                                continue;

                            var currentRowCost = words.Last();

                            if (words.Last().Contains('-'))
                            {
                                Regex rgx = new("[^a-zA-Z0-9 ,]");
                                currentRowCost = rgx.Replace(currentRowCost, "");
                                var negatedCost = decimal.Parse(currentRowCost, new CultureInfo("fi", true)) * -1;
                                productDictionary[previousProduct.Id].Cost = negatedCost;
                                continue;
                            }

                            if (words.FirstOrDefault() == "PANTTI" && !currentRowCost.Contains('-'))
                            {
                                productDictionary[previousProduct.Id].Cost = decimal.Parse(currentRowCost, new CultureInfo("fi", true));
                                continue;
                            }

                            var product = new Product
                            {
                                Id = Guid.NewGuid().ToString(),
                                Name = string.Join(" ", words.SkipLast(1)),
                                Cost = decimal.Parse(words.Last(), new CultureInfo("fi", true))
                            };
                            productDictionary.Add(product.Id, product);

                            previousProduct = product;
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Error: Something went wrong during the parsing of the products. The error occurred during parsing the row: '{String.Join(" ", rowWords)}' ", ex);
                        }
                    }
                }
            }
            return receipt;
        }

        private void PrintReceipt(Dictionary<string, Payer> payersDict, bool printExtended = false)
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

        private void AddProductToPayer(Dictionary<string, Payer> payersDict, string payer, Product product, Receipt receipt)
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