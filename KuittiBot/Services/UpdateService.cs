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
using OpenAI;
using Azure;
using OpenAI.Chat;
using OpenAI.Models;
using Message = OpenAI.Chat.Message;
using KuittiBot.Functions.Infrastructure;
using OpenAI.Threads;

namespace KuittiBot.Functions.Services
{
    public class UpdateService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ILogger<UpdateService> _logger;
        private IUserDataCache _userDataCache;
        private IUserFileInfoCache _userFileInfoCache;
        private IReceiptParsingService _receiptParsingService;
        private static bool _isLocal = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
        private static string _testikuitti = "maukan_kuitti.pdf";
        private readonly OpenAIClient _openAiClient;
        private UserSessionInfo _currentUser;

        public UpdateService(ITelegramBotClient botClient, ILogger<UpdateService> logger, IUserDataCache userDataCache, IUserFileInfoCache userFileInfoCache, IReceiptParsingService receiptParsingService)
        {
            _botClient = botClient;
            _logger = logger;
            _userDataCache = userDataCache;
            _userFileInfoCache = userFileInfoCache;
            _receiptParsingService = receiptParsingService;
            _openAiClient = new OpenAIClient(OpenAIAuthentication.LoadFromEnv());
        }

            public async Task InitializeParseingForUser(Update update)
        {
            if (!(update.Message is { } message)) return;

            _currentUser = new UserSessionInfo() 
            {
                UserId = message.From.Id.ToString(),
                FileId = update.Message.Document?.FileId ?? update.Message.Photo?.LastOrDefault().FileId
            };

            var stream = await DownloadFileAsync(update);
            await UploadFileToStorage(update, stream);

            Receipt receipt = new Receipt();
            receipt = await _receiptParsingService.ParseProductsFromReceiptImageAsync(stream);

            _currentUser.Confidence = receipt.Confidence;

            // TODO insert confidence to the storage _currentUser.Confidence.ToString("0.0000"),

            await _userFileInfoCache.UpdateSuccessState(_currentUser.Hash, true);

            await PrintReceiptToUser(update, receipt);
        }


        private async Task UploadFileToStorage(Update update, Stream stream)
        {
            stream.Position = 0;

            _currentUser.DocumentType = update.Message?.Document?.MimeType ?? "application/jpg";

            var fileHash = ComputeHash(stream);
            _currentUser.Hash = fileHash;
            _currentUser.FileName = fileHash +  (_currentUser.DocumentType == "application/jpg" ? ".jpg" : ".pdf");

            var uploader = new AzureBlobUploader();
            await uploader.UploadFileStreamIfNotExistAsync("receipt-cache", _currentUser.FileName, stream, _currentUser.DocumentType);

            var userInfoToUpload = new UserFileInfoEntity
            {
                UserId = _currentUser.UserId,
                FileName = _currentUser.FileName,
                FileId = _currentUser.FileId,
                Hash = _currentUser.Hash,
                SuccessFullyParsed = false
            };

            await _userFileInfoCache.InsertUserFileInfoIfNotExistAsync(userInfoToUpload);
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
            if (!_isLocal)
            {
                await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"Moro {message.From.FirstName ?? message.From.Username}! \n" +
                      $"Parseen sun kuitin bro!");
            }



            //await _botClient.SendTextMessageAsync(
            //    chatId: message.Chat.Id,
            //    text: "Hell yeah",
            //    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: CreateButton());
        }

        public async Task PrintLeaderboard(Update update)
        {
            if (!(update.Message is { } message)) return;

            var leaderboard = new Dictionary<string, int>();

            var allUsers = await _userDataCache.GetAllUsers();
            foreach (var user in allUsers)
            {
                var fileCount = await _userFileInfoCache.GetFileCountByUserId(user.Id);

                leaderboard.Add(user.UserName, fileCount);
            }

            // Sort the dictionary and take the top 10 users
            var topUsers = leaderboard.OrderByDescending(pair => pair.Value).Take(10);
            // Create the formatted string
            var leaderboardToPrint = string.Join("\n", topUsers.Select(userinfo => $"{userinfo.Key}: {userinfo.Value}"));

            Console.WriteLine($"T�ss� t�m�n hetken tulokset:\n{leaderboardToPrint}");

            if (!_isLocal)
            {
                await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"T�ss� t�m�n hetken tulokset:\n{leaderboardToPrint}");
            }

            //await _botClient.SendTextMessageAsync(
            //    chatId: message.Chat.Id,
            //    text: "Hell yeah",
            //    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: CreateButton());
        }

        public async Task PrintReceiptToUser(Update update, Receipt receipt)
        {
            if (!(update.Message is { } message)) return;

            List<string> receiptItems = receipt.Products.Select(x => $"{x.Name} - {x.Cost}").ToList();
            var str = receiptItems.Aggregate((a, x) => a + "\n" + x) + $"\n ------------------- \nYHTEENS�: {receipt.GetReceiptTotalCost()}";
            Console.WriteLine(str);


            if (!_isLocal)
            {
                await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"T�ss� kuitin ostokset: \n{str}",
                    parseMode: ParseMode.Html);

                var messages = new List<Message>
                {
                    new Message(Role.System, "Toimi luovana ja karismaattisena keskustelukumppanina, joka arvostelee ja kritisoi muiden ihmisten ostoksia. Teht�v�n�si on olla ankara kriitikko ostoksilleni. Selit� omin sanoin, miksi ostokseni ovat hyvi� tai huonoja. Haluan, ett� vakuutat minut siit�, miksi minun tulisi parantaa ostoksiani. Tee siit� jotenkin hauskaa. Mutta pid� se lyhyen� ja shokeeraavana. �l� kerro minulle, ett� yrit�t olla hauska tai shokeeraava. Haluan, ett� arvostelet ja tuomitset seuraavat ostokseni, jotka annan sinulle seuraavassa kehotteessa. Haluan ett� tuomitset satunnaisesti jonkun ostoksen (�l� valitse aina vain ensimm�ist� tai toista ostosta vaan valitse sellaiset tuotteet joista saa kaikista hauskimman tuomitsevan kommentin). Anna vastaus maksimissaa kolmella lauseella."),
                    new Message(Role.User, $"Hei katso miten hienoja ostoksia tein: {str}")
                };

                var chatRequest = new ChatRequest(messages, Model.GPT3_5_Turbo);
                var response = await _openAiClient.ChatEndpoint.GetCompletionAsync(chatRequest);
                var choice = response.FirstChoice;
                Console.WriteLine($"[{choice.Index}] {choice.Message.Role}: {choice.Message} | Finish Reason: {choice.FinishReason}");

                await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: choice.Message,
                    parseMode: ParseMode.Html);
            }
        }


        public async Task SayHello(Update update)
        {
            _logger.LogInformation("Invoke telegram update function");

            if (update.CallbackQuery.IsDefined())
            {

            }

            if (!(update.Message is { } message))
                return;


            var userFromCache = await _userDataCache.GetUserByIdAsync(message.From.Id.ToString());

            _logger.LogInformation("Received Message from {0}", message.Chat.Id);
            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"L�ytyy cachesta: '{userFromCache.UserName}' - chatId: '{message.Chat.Id}'");



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

                var fileWasCopied = await uploader.CopyFileToAnotherContainerIfNotExist("receipt-cache", "kuittibot-training", _currentUser.FileName);

                if (fileWasCopied)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: $"Tiedosto '{_currentUser.FileName}' siirrettiin training dataan");
                }
                await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"Voi jummijammi {message.From.FirstName ?? message.From.Username}! \n" +
                          $"Tuli t�mm�nen errori: \n" + exception.Message);

                var messages = new List<Message>
                {
                    new Message(Role.System, "Kuvittele ett� olet henkil�, joka on mestari keksim��n t�ysin absurdeja ja kaukaa haettuja selityksi� pienimmillekin virheilleen. Esimerkiksi t�m� henkil� ei koskaan vain my�h�sty, vaan h�nen my�h�stymisens� johtuu aina jostain uskomattoman ep�todenn�k�isest� tapahtumasarjasta. Esimerkiksi, jos h�n my�h�styy t�ist�, h�nen selityksens� ei ole ruuhka tai unohdettu her�tyskello. Sen sijaan h�n kertoo tarinan siit�, kuinka h�n kohtasi matkalla puhuvan papukaijan, joka oli eksynyt ja joka vei h�net v��r��n suuntaan antaessaan suuntavihjeit�. Tai kun h�n unohtaa palauttaa t�rke�n asiakirjan, h�n ei vain sano unohtaneensa sit�. H�n kertoo kuinka asiakirja \"varastettiin\" salaper�isen n�kym�tt�m�n agentin toimesta, joka sekoittaa s��nn�llisesti h�nen papereitaan. T�m� henkil� on luova, ehk� v�h�n teatraalinenkin, ja h�nen tarinansa ovat niin v�rikk�it� ja mielikuvituksellisia, ett� niit� on vaikea ottaa vakavasti. H�n ei ehk� edes odota, ett� muut uskovat n�ihin kertomuksiin; ne ovat pikemminkin h�nen tapansa v�ltell� vastuuta tai lis�t� huumoria jokap�iv�isiin tilanteisiin. H�nell� on taipumus olla keskipisteen�, ei ainoastaan h�nen tarinoidensa fantastisuuden vuoksi, vaan my�s siksi, ett� ihmiset odottavat innolla, mit� h�n seuraavaksi keksii. Haluan ett� kun annan sinulle ohjelmistossani tapahtuvan virheen viestin, niin olet t�llainen yll� kuvaamani henkil� ja keksit t�lle virheelle jonkin todella absurdin ja kaukaa haetun selityksen painottaen ett� ohjelmiston koodissa tai sen toiminnassa ei ole mit��n vikaa. �l� kerro minulle, ett� yrit�t keksi� kaukaa haettuja tai absurdeja selityksi�. Vastaa Suomen kielell�."),
                    new Message(Role.User, $"Koodi palautti virheen yritt�ess� k�yd� kuittia l�pi - {exception.Message}")
                };

                var chatRequest = new ChatRequest(messages, Model.GPT3_5_Turbo);
                var response = await _openAiClient.ChatEndpoint.GetCompletionAsync(chatRequest);
                var choice = response.FirstChoice;
                Console.WriteLine($"[{choice.Index}] {choice.Message.Role}: {choice.Message} | Finish Reason: {choice.FinishReason}");

                await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: choice.Message,
                    parseMode: ParseMode.Html);
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

        public async Task CorrectTrainingData(Update update)
        {
            if (!(update.Message is { } message)) return;
            
            var uploader = new AzureBlobUploader();
            var fileNumber = await uploader.CorrectTrainingLabelJson("kuittibot-training");

            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"{fileNumber} training files were corrected",
                parseMode: ParseMode.Html);
        }
    }
}