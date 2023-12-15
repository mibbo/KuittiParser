using Azure.Storage.Blobs;
using KuittiBot.Functions.Domain.Abstractions;
using KuittiBot.Functions.Domain.Models;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.SystemFunctions;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using KuittiBot.Functions.Services;
using Telegram.Bot.Types.Enums;
using System.Web;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace KuittiBot.Functions.Services
{
    public class UpdateService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ILogger<UpdateService> _logger;
        private IUserDataCache _userDataCache;
        private IFileHashCache _fileHashCache;
        private IReceiptParsingService _receiptParsingService;
        private static string _fileName;
        private static bool _isLocal = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
        private static string _testikuitti = "maukan_kuitti.pdf";


        public UpdateService(ITelegramBotClient botClient, ILogger<UpdateService> logger, IUserDataCache userDataCache, IFileHashCache fileHashCache, IReceiptParsingService receiptParsingService)
        {
            _botClient = botClient;
            _logger = logger;
            _userDataCache = userDataCache;
            _fileHashCache = fileHashCache;
            _receiptParsingService = receiptParsingService;
        }

            public async Task InitializeParseingForUser(Update update)
        {
            if (!(update.Message is { } message)) return;

            var stream = await DownloadFileAsync(update);
            await UploadFileIfDoesNotExist(update, stream);

            Receipt receipt = new Receipt();
            receipt = await _receiptParsingService.ParseProductsFromReceiptImageAsync(stream);

            await PrintReceiptToUser(update, receipt);
        }


        private async Task UploadFileIfDoesNotExist(Update update, Stream stream)
        {
            stream.Position = 0;

            var documentType = update.Message?.Document?.MimeType ?? "application/jpg";

            var uploader = new AzureBlobUploader();

            var fileHash = ComputeHash(stream);
            _fileName = fileHash + (documentType == "application/jpg" ? ".jpg" : ".pdf");

            var checkIfHashExists = await _fileHashCache.GetFileByHash(fileHash);
            if (checkIfHashExists == null)
            {
                await uploader.UploadFileStreamAsync("receipt-cache", _fileName, stream, documentType);
                await _fileHashCache.InsertFileHashAsync(_fileName, fileHash);
            }
        }


        private async Task<Stream> DownloadFileAsync(Update update)
        {
            var fileId = update.Message?.Document?.FileId ?? update.Message.Photo.LastOrDefault().FileId;

            string response = _isLocal ? "Function is running on local environment." : "Function is running on Azure.";
            Console.WriteLine(response);

            Stream stream = new MemoryStream();

            if (_isLocal)
            {
                //test document
                var path = @$"C:\Users\tommi.mikkola\git\Projektit\KuittiParser\KuittiParses.Console\Kuitit\{_testikuitti}"; 
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    await fs.CopyToAsync(stream);
                }
            }
            else
            {
                _ = await _botClient.GetInfoAndDownloadFileAsync(
                    fileId: fileId,
                    destination: stream);
            }

            return stream;
        }


        private static string ComputeHash(Stream stream)
        {
            using (var sha256 = SHA256.Create())
            {
                // Reset the position of the stream to ensure it's read from the start
                stream.Position = 0;

                // Compute the hash of the stream
                byte[] hash = sha256.ComputeHash(stream);

                // Convert the byte array to a hexadecimal string
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }


        public async Task WelcomeUser(Update update)
        {
            _logger.LogInformation("Invoke telegram update function");

            if (!(update.Message is { } message)) return;

            _logger.LogInformation("Received Message from {0}", message.Chat.Id);
            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"Moro {message.From.FirstName ?? message.From.Username}! \n" +
                      $"Parseen sun kuitin bro!");



            //await _botClient.SendTextMessageAsync(
            //    chatId: message.Chat.Id,
            //    text: "Hell yeah",
            //    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: CreateButton());
        }

        public async Task PrintReceiptToUser(Update update, Receipt receipt)
        {
            if (!(update.Message is { } message)) return;
            
            List<string> receiptItems = receipt.Products.Select(x => $"{x.Name} - {x.Cost}").ToList();
            var str = receiptItems.Aggregate((a, x) => a + "\n" + x) + $"\n ------------------- \nYHTEENSÄ: {receipt.GetReceiptTotalCost()}";
            Console.WriteLine(str);

            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"Tässä kuitin ostokset: \n{str}",
                parseMode: ParseMode.Html);
        }


        public async Task SayHello(Update update)
        {
            _logger.LogInformation("Invoke telegram update function");

            if (update.CallbackQuery.IsDefined())
            {

            }

            if (!(update.Message is { } message))
                return;


            var userFromCache = await _userDataCache.GetUserById(message.From.Id.ToString());

            _logger.LogInformation("Received Message from {0}", message.Chat.Id);
            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"Löytyy cachesta: '{userFromCache.UserName}' - chatId: '{message.Chat.Id}'");



            //await _botClient.SendTextMessageAsync(
            //    chatId: message.Chat.Id,
            //    text: "Hell yeah",
            //    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: CreateButton());
        }


        public async Task LogError(Update update, Exception exception)
        {
            _logger.LogInformation("Invoke telegram update function");

            if (update is null)
                return;

            if (!(update.Message is { } message)) return;

            if (!_isLocal)
            {
                var uploader = new AzureBlobUploader();


                var fileWasCopied = await uploader.CopyFileToAnotherContainer("receipt-cache", "kuittibot-training", _fileName);

                if (fileWasCopied)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: $"Tiedosto '{_fileName}' siirrettiin training dataan");
                } 

                _logger.LogInformation("Received Message from {0}", message.Chat.Id);
                await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"Voi jummijammi {message.From.FirstName ?? message.From.Username}! \n" +
                          $"Tuli tämmönen errori: \n" + exception.Message);
            }

        }


        public static InlineKeyboardMarkup CreateButton()
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
{
                        new [] // first row
                        {
                            InlineKeyboardButton.WithCallbackData("1.1"),
                            InlineKeyboardButton.WithCallbackData("1.2"),
                        },
                        new [] // second row
                        {
                            InlineKeyboardButton.WithCallbackData("2.1"),
                            InlineKeyboardButton.WithCallbackData("2.2"),
                        }
                    });

            return inlineKeyboard;
        }
    }
}