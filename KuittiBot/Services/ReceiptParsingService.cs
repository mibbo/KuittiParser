using System;
using System.Data;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig;
using KuittiBot.Functions.Domain.Models;
using System.IO;
using System.Globalization;
using KuittiBot.Functions.Domain.Abstractions;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure;
using System.Net;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;

namespace KuittiBot.Functions.Services
{
    public class ReceiptParsingService : IReceiptParsingService
    {
        private static string _aiKey = Environment
                .GetEnvironmentVariable("FormRecognizerAiKey", EnvironmentVariableTarget.Process)
                ?? throw new ArgumentException("Can not get FormRecognizerAiKey. Set token in environment setting");
        private static string _aiUrl = Environment
                .GetEnvironmentVariable("FormRecognizerAiEndpoint", EnvironmentVariableTarget.Process)
                ?? throw new ArgumentException("Can not get FormRecognizerAiEndpoint. Set token in environment setting");

        public ReceiptParsingService()
        {
        }

        public async Task<Receipt> ParseProductsFromReceiptImageAsync(Stream stream)
        {
            stream.Position = 0;

            DocumentAnalysisClient client = new DocumentAnalysisClient(new Uri(_aiUrl), new AzureKeyCredential(_aiKey));


            AnalyzeDocumentOperation operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "Kuittibot_v3", stream);

            AnalyzeResult result = operation.Value;

            List<string> lines = result.Pages.FirstOrDefault().Lines.Select(x => x.Content).ToList();

            var receipt = new Receipt();

            for (int i = 0; i < result.Documents.Count; i++)
            {
                AnalyzedDocument document = result.Documents[i];

                receipt.ShopName = document.Fields["MerchantName"].Value.AsString();

                List<Product> products = new List<Product>();

                var receiptItems = document.Fields["Items"].Value.AsList();

                foreach (DocumentField productField in receiptItems)
                {
                    IReadOnlyDictionary<string, DocumentField> productData = productField.Value.AsDictionary();

                    try
                    {
                        var currentProductCostString = productData["TotalPrice"].Content;
                        var currentProduct = new Product
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = productData["Description"].Value.AsString(),
                            Cost = ParseDecimalCost(currentProductCostString)
                        };

                        if (productData.ContainsKey("Discount"))
                        {
                            var discountsRaw = productData["Discount"].Content;
                            currentProduct.Discounts = ConvertToDecimalList(discountsRaw, "\n");
                        }

                        products.Add(currentProduct);
                    } 
                    catch (Exception e)
                    {
                        throw new Exception($"Problems with product: '{productData["Description"].Value.AsString()}'", e);
                    }

                }
                receipt.Products = products;

                // Set and verify receipt total cost
                receipt.RawTotalCost = ParseDecimalCost(document.Fields["Total"].Content);
                var calculatedTotalCost = receipt.GetReceiptTotalCost();
                if (calculatedTotalCost != receipt.RawTotalCost)
                {
                    throw new Exception($"Error: Something went wrong during the product parsing. Program calculated total cost is '{calculatedTotalCost}' when the actual cost in the receipt is '{receipt.RawTotalCost}'");
                }
                // Set and verify receipt total cost
                //receipt.RawTotalCost = decimal.Parse(document.Fields["Total"].Content, new CultureInfo("fi", true));
                //var calculatedTotalCost = receipt.GetReceiptTotalCost();
                //if (calculatedTotalCost != receipt.RawTotalCost)
                //{
                //    throw new Exception($"Error: Something went wrong during the product parsing. Program calculated total cost is '{calculatedTotalCost}' when the actual cost in the receipt is '{receipt.RawTotalCost}'");
                //}

            }
            return receipt;
        }

        static decimal ParseDecimalCost(string costString)
        {
            // Replace dot with comma if present
            if (costString.Contains('.'))
            {
                costString = costString.Replace('.', ',');
            }
            return decimal.Parse(costString, new CultureInfo("fi", true));
        }

        private static List<decimal> ConvertToDecimalList(string input, string delimeter)
        {
            List<decimal> resultList = new List<decimal>();

            var discountsRaw = input.Replace(" ", string.Empty);
            var discounts = discountsRaw.Split(delimeter).ToList();
            foreach (var discount in discounts)
            {
                resultList.Add(RemoveMinusSignFromNumber(discount));
            }
            return resultList;
        }

        private static decimal RemoveMinusSignFromNumber(string number)
        {
            return decimal.Parse(number.Replace("-", string.Empty), new CultureInfo("fi", true)) * -1;
        }


        public async Task<Receipt> ParseProductsFromReceiptImageAsync_old(Stream stream)
        {

            //AzureKeyCredential credential = new AzureKeyCredential(_aiKey);
            DocumentAnalysisClient client = new DocumentAnalysisClient(new Uri(_aiUrl), new AzureKeyCredential(_aiKey));

            AnalyzeDocumentOperation operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-receipt", stream);

            AnalyzeResult result = operation.Value;

            List<string> lines = result.Pages.FirstOrDefault().Lines.Select(x => x.Content).ToList();

            var receipt = new Receipt();

            for (int i = 0; i < result.Documents.Count; i++)
            {
                AnalyzedDocument document = result.Documents[i];

                receipt.ShopName = document.Fields["MerchantName"].Value.AsString();

                Dictionary<string, Product> productDictionary = new Dictionary<string, Product>();

                var receiptItems = document.Fields["Items"].Value.AsList();
                var firstProductName = receiptItems.FirstOrDefault().Value.AsDictionary()["Description"].Value.AsString();
                List<string> productCostList = ReturnProductCostList(lines, firstProductName);

                var previousProduct = new Product();

                //List<string> productCostList = new List<string> { "1", "1.1", "2", "2.2" };  // Longer AKA productCostList
                //List<string> receiptItems = new List<string> { "1", "2", "2.2" }; // shorter AKA AI list

                int c = 0;  // cost list index
                int p = 0; // product list index
                while (i < productCostList.Count-1 && p < receiptItems.Count)
                {
                    var cCost = productCostList[c];
                    var pCost = receiptItems[p].Value.AsDictionary()["TotalPrice"].Content;

                    // Process matching items


                    IReadOnlyDictionary<string, DocumentField> productData = receiptItems[p].Value.AsDictionary();

                    var currentProductCostString = productData["TotalPrice"].Content;



                    if (cCost == pCost)
                    {
                        var currentProduct = new Product
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = productData["Description"].Value.AsString(),
                            Cost = decimal.Parse(currentProductCostString, new CultureInfo("fi", true))
                        };

                        string lastNumberInContent = ExtractLastNumber(receiptItems[p].Content, currentProductCostString);

                        Console.WriteLine(currentProduct.Name);
                        Console.WriteLine("  -Cost " + pCost);
                        // Check if the AI recognized product field contains also the discount (it sometimes recognizes discounts as separate product but not always...)
                        if (lastNumberInContent != currentProductCostString)
                        {
                            var originalCost = currentProduct.Cost;
                            var discount = decimal.Parse(lastNumberInContent, new CultureInfo("fi", true));
                            Console.WriteLine("   -Current discount: " + discount);

                            currentProduct.Cost = discount;
                            Console.WriteLine("  -New cost: " + currentProduct.Cost);
                            c++;
                        }

                        // If the AI recognizes a discount as a standalone product (then it is negated from previous product)
                        if (currentProductCostString.Contains('-'))
                        {
                            Regex rgx = new("[^a-zA-Z0-9 ,]");
                            currentProductCostString = rgx.Replace(currentProductCostString, "");
                            Console.WriteLine("   -Current discount: -" + currentProductCostString);
                            var negatedCost = decimal.Parse(currentProductCostString, new CultureInfo("fi", true)) * -1;
                            productDictionary[previousProduct.Id].Cost = negatedCost;
                            Console.WriteLine("  -New cost: " + productDictionary[previousProduct.Id].Cost);
                            c++;
                            p++;
                            continue;
                        }

                        if (currentProduct.Name.Contains("PANTTI") && !currentProductCostString.Contains('-'))
                        {
                            productDictionary[previousProduct.Id].Cost = currentProduct.Cost;
                            c++;
                            p++;
                            continue;
                        }
                        productDictionary.Add(currentProduct.Id, currentProduct);
                        previousProduct = currentProduct;

                        c++;
                        p++;
                    }
                    else
                    {
                        // Process only the discount items that the AI does not recognize

                        Regex rgx = new("[^a-zA-Z0-9 ,]");
                        cCost = rgx.Replace(cCost, "");
                        Console.WriteLine("   -Current discount: -" + cCost);
                        var negatedCost = decimal.Parse(cCost, new CultureInfo("fi", true)) * -1;
                        productDictionary[previousProduct.Id].Cost = negatedCost;

                        Console.WriteLine("  -New cost: " + productDictionary[previousProduct.Id].Cost);

                        c++;
                    }
                }

                // Handle any remaining items in cost list (all discounts that are after the last product that AI recognizes)
                while (c < productCostList.Count -1)
                {
                    var cCost = productCostList[c];

                    Regex rgx = new("[^a-zA-Z0-9 ,]");
                    cCost = rgx.Replace(cCost, "");
                    Console.WriteLine("   -Current discount: -" + cCost);
                    var negatedCost = decimal.Parse(cCost, new CultureInfo("fi", true)) * -1;
                    productDictionary[previousProduct.Id].Cost = negatedCost;

                    Console.WriteLine(" - New cost: " + productDictionary[previousProduct.Id].Cost);

                    c++;
                }
                receipt.Products = productDictionary.Select(p => p.Value).ToList();

                var totalCost = decimal.Parse(productCostList[c], new CultureInfo("fi", true));
                Console.WriteLine("Total cost: " + totalCost.ToString());

                // Set and verify receipt total cost
                receipt.RawTotalCost = decimal.Parse(document.Fields["Total"].Content, new CultureInfo("fi", true));
                var calculatedTotalCost = receipt.GetReceiptTotalCost();
                if (calculatedTotalCost != receipt.RawTotalCost && calculatedTotalCost != totalCost)
                {
                    throw new Exception($"Error: Something went wrong during the product parsing. Program calculated total cost is '{calculatedTotalCost}' when the actual cost in the receipt is '{receipt.RawTotalCost}'");
                }

                return receipt;



                foreach (DocumentField productField in receiptItems)
                {
                    IReadOnlyDictionary<string, DocumentField> productData = productField.Value.AsDictionary();

                    var currentProductCostString = productData["TotalPrice"].Content;
                    var currentProduct = new Product
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = productData["Description"].Value.AsString(),
                        Cost = decimal.Parse(currentProductCostString, new CultureInfo("fi", true))
                    };

                    string lastNumberInContent = ExtractLastNumber(productField.Content, currentProductCostString);

                    // Check if the AI recognized product field contains also the discount (it sometimes recognizes discounts as separate product but not always...)
                    if (lastNumberInContent != currentProductCostString)
                    {
                        var originalCost = currentProduct.Cost;
                        var discount = decimal.Parse(lastNumberInContent, new CultureInfo("fi", true));

                        currentProduct.Cost = discount;
                    }

                    // Not sure if this is needed
                    if (currentProductCostString.Contains('-'))
                    {
                        //Regex rgx = new("[^a-zA-Z0-9 ,]");
                        //currentProductCost = rgx.Replace(currentProductCost, "");
                        //var negatedCost = decimal.Parse(currentProductCost, new CultureInfo("fi", true)) * -1;
                        //productDictionary[previousProduct.Id].Cost = negatedCost;
                        //continue;
                    }

                    if (currentProduct.Name.Contains("PANTTI") && !currentProductCostString.Contains('-'))
                    {
                        productDictionary[previousProduct.Id].Cost = currentProduct.Cost;
                        continue;
                    }

                    //currentProduct.Cost = decimal.Parse(currentProductCost, new CultureInfo("fi", true));

                    productDictionary.Add(currentProduct.Id, currentProduct);
                    previousProduct = currentProduct;
                }
                receipt.Products = productDictionary.Select(p => p.Value).ToList();


                // Set and verify receipt total cost
                //receipt.RawTotalCost = decimal.Parse(document.Fields["Total"].Content, new CultureInfo("fi", true));
                //var calculatedTotalCost = receipt.GetReceiptTotalCost();
                //if (calculatedTotalCost != receipt.RawTotalCost)
                //{
                //    throw new Exception($"Error: Something went wrong during the product parsing. Program calculated total cost is '{calculatedTotalCost}' when the actual cost in the receipt is '{receipt.RawTotalCost}'");
                //}

            }
            return receipt;
        }

        private static List<string> ReturnProductCostList(List<string> lines, string firstProduct)
        {
            // Find the index of the specified start and end strings
            int startIndex = lines.FindIndex(item => item == firstProduct);
            int endIndex = lines.FindIndex(item => item == "YHTEENSÄ");

            // If either string is not found, or if start comes after end, do not process further
            if (startIndex == -1 || endIndex == -1 || startIndex > endIndex)
            {
                throw new Exception($"Error: Something went wrong during the product parsing. Invalid receipt structure.");
            }

            // Select only the items between these two indices
            var relevantItems = lines.Skip(startIndex).Take(endIndex - startIndex + 2).ToList();

            // Regular expression to match a double value. It accounts for a comma as a decimal separator and an optional trailing hyphen.
            Regex doubleRegex = new Regex(@"^\d+,\d+(-)?$");

            var productCostList = relevantItems.Where(item => doubleRegex.IsMatch(item)).ToList();

            return productCostList;
        }
        private static string ExtractLastNumber(string content, string cost)
        {
            // Regex to find the last number in the string. It handles negative numbers as well.
            var match = Regex.Match(content, @"([0-9,]+-?)$");

            var lastNumber = (content.EndsWith("-") && match.Success) ? match.Groups[1].Value : cost;

            return lastNumber;
        }

        public Receipt ParseProductsFromReceiptPdf(Stream stream)
        {
            List<string> supportedShops = new List<string> { "K-Citymarket", "K-Market", "Prisma", "S-market", "Sale", "Sokos" };

            Dictionary<string, double> shopReceiptCoordinates = new Dictionary<string, double>
            {
                { "K-Citymarket", 441.57807548136384},
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
                //foreach (Page page in document.GetPages())
                //{ 
                //}
                
                var firstPageOnly = document.GetPage(1); // Does not support multiple pages long receipts (Kesko does this...) could these be attached as one? or handled by each page hmm?

                List<List<Word>> rowList = new();

                var wordList = firstPageOnly.GetWords().ToList();

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

                        var currentProductCost = words.Last();

                        if (currentProductCost.Contains('-'))
                        {
                            Regex rgx = new("[^a-zA-Z0-9 ,]");
                            currentProductCost = rgx.Replace(currentProductCost, "");
                            var negatedCost = decimal.Parse(currentProductCost, new CultureInfo("fi", true)) * -1;
                            productDictionary[previousProduct.Id].Cost = negatedCost;
                            continue;
                        }

                        if (words.FirstOrDefault() == "PANTTI" && !currentProductCost.Contains('-'))
                        {
                            productDictionary[previousProduct.Id].Cost = decimal.Parse(currentProductCost, new CultureInfo("fi", true));
                            continue;
                        }

                        var currentProduct = new Product
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = string.Join(" ", words.SkipLast(1)),
                            Cost = decimal.Parse(currentProductCost, new CultureInfo("fi", true))
                        };
                        productDictionary.Add(currentProduct.Id, currentProduct);

                        previousProduct = currentProduct;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error: Something went wrong during the parsing of the products. The error occurred during parsing the row: '{String.Join(" ", rowWords)}' ", ex);
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