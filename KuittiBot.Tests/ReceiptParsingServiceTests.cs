using KuittiBot.Functions.Domain.Models;
using KuittiBot.Functions.Services;
using Xunit;

namespace KuittiBot.Tests
{
    public class ReceiptParsingServiceTests
    {
        [Fact]
        public void ParseProductsFromReceipt_WhenCalledWithValidReceipt_ReturnsExpectedProducts()
        {
            // Arrange
            var service = new ReceiptParsingService();
            var testReceiptDir = @"C:\Users\tommi.mikkola\git\Projektit\KuittiParser\KuittiParses.Console\Kuitit\";

            var pdfFiles = Directory.EnumerateFiles(testReceiptDir, "*.pdf");

            foreach (var pdfFile in pdfFiles)
            {
                using var stream = CreateMockStreamForPdf(pdfFile);

                // Act

                Receipt receipt = service.ParseProductsFromReceipt(stream);

                Assert.NotNull(receipt);

                var rawCost = receipt.GetReceiptTotalCost();
                var actualCost = receipt.RawTotalCost;

                Assert.Equal(rawCost, actualCost);
            }
        }

        private Stream CreateMockStreamForPdf(string filepath)
        {
            if (!File.Exists(filepath))
            {
                throw new FileNotFoundException($"The file at {filepath} was not found.");
            }

            var stream = new MemoryStream();
            using (var fileStream = new FileStream(filepath, FileMode.Open, FileAccess.Read))
            {
                fileStream.CopyTo(stream);
            }
            stream.Position = 0;

            return stream;
        }
    }
}