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

namespace KuittiBot.Functions.Services
{
    public class UpdateService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ILogger<UpdateService> _logger;
        private IUserDataCache _userDataCache;
        private IReceiptParsingService _receiptParsingService;

        public UpdateService(ITelegramBotClient botClient, ILogger<UpdateService> logger, IUserDataCache userDataCache, IReceiptParsingService receiptParsingService)
        {
            _botClient = botClient;
            _logger = logger;
            _userDataCache = userDataCache;
            _receiptParsingService = receiptParsingService;
        }

            public async Task InitializeParseingForUser(Update update)
        {
            if (!(update.Message is { } message)) return;

            var documentType = update.Message?.Document?.MimeType ?? "application/jpg";
            var fileId = update.Message?.Document?.FileId ?? update.Message.Photo.LastOrDefault().FileId;

            var receipt = await DownloadReceiptPdf(fileId, documentType);

            List<string> receiptItems = receipt.Products.Select(x => $"{x.Name} - {x.Cost}").ToList();
            var str = receiptItems.Aggregate((a, x) => a + "\n" + x) + $"\n ------------------- \nYHTEENSÄ: {receipt.GetReceiptTotalCost()}";
            Console.WriteLine(str);

            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"Tässä kuitin ostokset: \n{str}",
                parseMode: ParseMode.Html);



            //var newUser = new UserDataCacheEntity()
            //{
            //    Id = update.Message.From.Id.ToString(),
            //    FileName = update.Message.Document.FileName,
            //    FileId = update.Message.Document.FileId,
            //    UserName = update.Message.From.Username
            //};

            //await _userDataCache.UpdateUserStateAsync(newUser);
        }



        //var fileId = update.Message.Document.FileId;
        //var stream = await DownloadReceipt(fileId);
        private async Task<Receipt> DownloadReceiptPdf(string fileId, string documentType)
        {
            //var fileInfo = await _botClient.GetFileAsync(fileId);

            bool isLocal = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));

            string response = isLocal ? "Function is running on local environment." : "Function is running on Azure.";

            Stream stream = new MemoryStream();

            if (isLocal)
            {
                //test document
                var path = @"C:\Users\tommi.mikkola\git\Projektit\KuittiParser\KuittiParses.Console\Kuitit\Kuittibot_v3_testikuitti_pitka.jpg"; //Kuittibot_v3_testikuitti_kmarket.jpeg
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    fs.CopyTo(stream);
                }
            }
            else
            {
                _ = await _botClient.GetInfoAndDownloadFileAsync(
                    fileId: fileId,
                    destination: stream);

                stream.Position = 0;

                var uploader = new AzureBlobUploader();
                await uploader.UploadFileStreamAsync("kuittibot-training", fileId + (documentType == "application/jpg" ? ".jpg" : ".pdf"), stream, documentType);
            }

            stream.Position = 0;

            Receipt receipt = new Receipt();

            receipt = await _receiptParsingService.ParseProductsFromReceiptImageAsync(stream);



            //if (documentType == "application/pdf")
            //{
            //    receipt = _receiptParsingService.ParseProductsFromReceiptPdf(stream);
            //}
            //if (documentType == "application/jpg")
            //{
            //    receipt = await _receiptParsingService.ParseProductsFromReceiptImageAsync(stream);
            //}

            return receipt;
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

            _logger.LogInformation("Received Message from {0}", message.Chat.Id);
            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"Voi jummijammi {message.From.FirstName ?? message.From.Username}! \n" +
                      $"Tuli tämmönen errori: \n" + exception.Message);
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