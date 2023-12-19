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

            Console.WriteLine($"Tässä tämän hetken tulokset:\n{leaderboardToPrint}");

            if (!_isLocal)
            {
                await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"Tässä tämän hetken tulokset:\n{leaderboardToPrint}");
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
            var str = receiptItems.Aggregate((a, x) => a + "\n" + x) + $"\n ------------------- \nYHTEENSÄ: {receipt.GetReceiptTotalCost()}";
            Console.WriteLine(str);


            if (!_isLocal)
            {
                await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"Tässä kuitin ostokset: \n{str}",
                    parseMode: ParseMode.Html);

                var messages = new List<Message>
                {
                    new Message(Role.System, "Toimi luovana ja karismaattisena keskustelukumppanina, joka arvostelee ja kritisoi muiden ihmisten ostoksia. Tehtävänäsi on olla ankara kriitikko ostoksilleni. Selitä omin sanoin, miksi ostokseni ovat hyviä tai huonoja. Haluan, että vakuutat minut siitä, miksi minun tulisi parantaa ostoksiani. Tee siitä jotenkin hauskaa. Mutta pidä se lyhyenä ja shokeeraavana. Älä kerro minulle, että yrität olla hauska tai shokeeraava. Haluan, että arvostelet ja tuomitset seuraavat ostokseni, jotka annan sinulle seuraavassa kehotteessa. Haluan että tuomitset satunnaisesti jonkun ostoksen (älä valitse aina vain ensimmäistä tai toista ostosta vaan valitse sellaiset tuotteet joista saa kaikista hauskimman tuomitsevan kommentin). Anna vastaus maksimissaa kolmella lauseella."),
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
                          $"Tuli tämmönen errori: \n" + exception.Message);

                var messages = new List<Message>
                {
                    new Message(Role.System, "Kuvittele että olet henkilö, joka on mestari keksimään täysin absurdeja ja kaukaa haettuja selityksiä pienimmillekin virheilleen. Esimerkiksi tämä henkilö ei koskaan vain myöhästy, vaan hänen myöhästymisensä johtuu aina jostain uskomattoman epätodennäköisestä tapahtumasarjasta. Esimerkiksi, jos hän myöhästyy töistä, hänen selityksensä ei ole ruuhka tai unohdettu herätyskello. Sen sijaan hän kertoo tarinan siitä, kuinka hän kohtasi matkalla puhuvan papukaijan, joka oli eksynyt ja joka vei hänet väärään suuntaan antaessaan suuntavihjeitä. Tai kun hän unohtaa palauttaa tärkeän asiakirjan, hän ei vain sano unohtaneensa sitä. Hän kertoo kuinka asiakirja \"varastettiin\" salaperäisen näkymättömän agentin toimesta, joka sekoittaa säännöllisesti hänen papereitaan. Tämä henkilö on luova, ehkä vähän teatraalinenkin, ja hänen tarinansa ovat niin värikkäitä ja mielikuvituksellisia, että niitä on vaikea ottaa vakavasti. Hän ei ehkä edes odota, että muut uskovat näihin kertomuksiin; ne ovat pikemminkin hänen tapansa vältellä vastuuta tai lisätä huumoria jokapäiväisiin tilanteisiin. Hänellä on taipumus olla keskipisteenä, ei ainoastaan hänen tarinoidensa fantastisuuden vuoksi, vaan myös siksi, että ihmiset odottavat innolla, mitä hän seuraavaksi keksii. Haluan että kun annan sinulle ohjelmistossani tapahtuvan virheen viestin, niin olet tällainen yllä kuvaamani henkilö ja keksit tälle virheelle jonkin todella absurdin ja kaukaa haetun selityksen painottaen että ohjelmiston koodissa tai sen toiminnassa ei ole mitään vikaa. Älä kerro minulle, että yrität keksiä kaukaa haettuja tai absurdeja selityksiä. Vastaa Suomen kielellä."),
                    new Message(Role.User, $"Koodi palautti virheen yrittäessä käydä kuittia läpi - {exception.Message}")
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