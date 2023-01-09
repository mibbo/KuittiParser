
using KuittiParser;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;


internal class Program
{
    private static void Main(string[] args)
    {
        using (PdfDocument document = PdfDocument.Open(@"C:\Users\tommi.mikkola\git\Projektit\KuittiParser\KuittiParser\Kuitit\testikuitti.pdf"))
        {
            foreach (Page page in document.GetPages())
            {
                var wordList = page.GetWords().ToList();

                // Create Lists of words for each receipt row number
                List<List<Word>> rowList = wordList.GroupBy(it => it.BoundingBox.Bottom).Select(grp => grp.ToList()).ToList();
                //Dictionary<double, List<Word>> orderDictionary = wordList.GroupBy(it => it.BoundingBox.Bottom).ToDictionary(dict => dict.Key, dict => dict.Select(item => item).ToList());

                var previousProduct = new ProductModel();

                // Dictionary for products to be added
                Dictionary<string, ProductModel> productDictionary = new Dictionary<string, ProductModel>();

                // Loop rows
                foreach (var rowWords in rowList.Skip(4))
                {
                    var words = rowWords.Select(word => word.Text).ToList();

                    if (words.FirstOrDefault() == "YHTEENSÄ")
                    {
                        // TODO: looppaa ja laske yhteen että mätsääkö YHTEENSÄ ja käsiteltyjen rivien summat
                        break;
                    }

                    // Skip rows that are not product rows
                    if (rowWords.LastOrDefault().BoundingBox.Left != 192.62890625)
                        continue;

                    var currentRowCost = words.LastOrDefault();

                    if (words.LastOrDefault().Contains('-'))
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
                        Cost = decimal.Parse(words.LastOrDefault())
                    };
                    productDictionary.Add(product.Id, product);

                    previousProduct = product;
                }

                foreach (var item in productDictionary)
                {
                    Console.WriteLine(item.Value.Name + " " + item.Value.Cost);
                }

                //foreach (Word word in page.GetWords())
                //{
                //    if (word.BoundingBox.Height == previousWord.BoundingBox.Height)
                //    {

                //    }
                //    else
                //    {
                //        previousWord = word;
                //    }
                //    Console.WriteLine(word.Text);
                //}
            }
        }
    }
}