
using KuittiParser;
using System.Collections.Generic;
using System.Data;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;


using (PdfDocument document = PdfDocument.Open(@"C:\Users\tommi.mikkola\git\Projektit\KuittiParser\KuittiParser\Kuitit\testikuitti.pdf"))
{
    foreach (Page page in document.GetPages())
    {
        var wordList = page.GetWords().ToList();

        // Create Lists of words for each receipt row number
        List<List<Word>> rowList = wordList.GroupBy(it => it.BoundingBox.Bottom).Select(grp => grp.ToList()).ToList();

        foreach (var rowWords in rowList.Skip(4))
        {
            if (rowWords.LastOrDefault().Letters.Reverse().Skip(2).FirstOrDefault().Value != ",")
                continue;
            if (rowWords.FirstOrDefault().Text == "YHTEENSÄ")
            {
                break;
            }

            var words = rowWords.Select(word => word.Text).ToList();
            var productWords = words.Remove(words.LastOrDefault());

            var product = new ProductModel
            {
                Name = string.Join(" ", words)
            };

            Console.WriteLine(product.Name);
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