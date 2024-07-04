using KuittiBot.Functions.Domain.Abstractions;
using KuittiBot.Functions.Domain.Models;
using KuittiBot.Functions.Infrastructure;
using Microsoft.Azure.Documents.SystemFunctions;
using Microsoft.Extensions.Logging;
using OpenAI;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace KuittiBot.Functions.Services
{
    public class UpdateService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ILogger<UpdateService> _logger;
        //private IUserDataCache _userDataCache;
        private IReceiptSessionCache _receiptSessionCache;
        private IReceiptParsingService _receiptParsingService;
        private readonly OpenAIClient _openAiClient;
        private IUserDataRepository _userDataRepository;
        private IReceiptSessionRepository _receiptSessionRepository;

        private static bool _isLocal = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
        private static string _testikuitti = Environment.GetEnvironmentVariable("TestReceipt"); //"testikuitti_kmarket.pdf";
        private string _fileName = "";

        public UpdateService(ITelegramBotClient botClient, ILogger<UpdateService> logger,/* IUserDataCache userDataCache, */IUserDataRepository userDataRepository, IReceiptSessionCache receiptSessionCache, IReceiptSessionRepository receiptSessionRepository, IReceiptParsingService receiptParsingService)
        {
            _botClient = botClient;
            _logger = logger;
            //_userDataCache = userDataCache;
            _userDataRepository = userDataRepository;
            _receiptSessionCache = receiptSessionCache;
            _receiptSessionRepository = receiptSessionRepository;
            _receiptParsingService = receiptParsingService;
            _openAiClient = new OpenAIClient(OpenAIAuthentication.LoadFromEnv());
        }

        public async Task InitializeParsingForUser(Update update)
        {
            var message = CheckMessageValidity(update);
            //if (!(update.Message is { } message)) return;

            var stream = await DownloadFileAsync(update);

            SessionInfo currentSession = CreateSessionInfo(message, stream);

            await UploadFileAsync(update, stream, currentSession);

            // TODO remove
            var userInfoToUpload = new ReceiptSessionEntity
            {
                UserId = currentSession.UserId,
                FileName = currentSession.FileName,
                Hash = currentSession.Hash,
                SessionSuccessful = false
            };
            _fileName = userInfoToUpload.FileName;

            //await _receiptSessionCache.InsertSessionIfNotExistAsync(userInfoToUpload);

            var sessionId = await _receiptSessionRepository.InitializeSession(currentSession);

            await _userDataRepository.SetNewSessionForUserAsync(sessionId, currentSession.UserId);

            Receipt receipt = await _receiptParsingService.ParseProductsFromReceiptImageAsync(stream);
            receipt.SessionId = sessionId;
            receipt.SessionSuccessful = false;

            // TODO remove
            //await _receiptSessionCache.UpdateSessionSuccessState(currentSession.Hash, true);
            //

            currentSession.SessionSuccessful = true;
            currentSession.ShopName = receipt.ShopName;
            currentSession.RawTotalCost = receipt.RawTotalCost;

            await _receiptSessionRepository.SaveReceiptAsync(receipt);

            await PrintReceiptToUser(update, receipt);

            await AskPayers(update);
        }

        public async Task HandlePayersAndAskFirstProduct(Update update)
        {
            var message = CheckMessageValidity(update);
            //if (!(update.Message is { } message)) return;
            var payersRaw = message.Text;
            var currentUser = message.From.Id.ToString();
            var sessionId = _userDataRepository.GetCurrentSessionByIdAsync(currentUser).Result;

            bool groupMode = await _receiptSessionRepository.IsGroupModeEnabledAsync(sessionId);

            if (payersRaw.Contains(':'))
            {
                groupMode = true;
                await _receiptSessionRepository.SetGroupModeForCurrentSession(sessionId, true);
            }

            if (groupMode)
            {
                // Atoli: tommi allu liisa, liha: maukka ville, kasvis: emma jasu

                var groups = ParseGroups(payersRaw);

                foreach (var group in groups)
                {
                    await AddSessionPayers(group.Value, currentUser);


                    // Just for debugging
                    Console.WriteLine($"Group: {group.Key}");
                    foreach (var member in group.Value)
                    {
                        Console.WriteLine($"  Member: {member}");
                    }
                }


                await AddSessionGroups(groups, currentUser);
            }
            else
            {
                List<string> payers = payersRaw.Split(" ").ToList();

                await AddSessionPayers(payers, currentUser);
            }

            await AskNextProduct(update);
        }

        public Dictionary<string, List<string>> ParseGroups(string payersRaw)
        {
            Dictionary<string, List<string>> groups = new Dictionary<string, List<string>>();

            payersRaw = "Atoli: tommi allu liisa, liha: maukka ville, kasvis: emma jasu";

            if (!payersRaw.Contains(':') || !payersRaw.Contains(','))
            {
                throw new Exception("Wrong payer/group format. For payers: 'tommi maukka pena'. For groups: 'liha: maukka ville, kasvis: emma jasu'.");
            }

            var fullGroups = payersRaw.Split(',').ToList();
            
            foreach (var group in fullGroups)
            {
                var groupData = group.Split(':');

                if (groupData.Length != 2)
                {
                    throw new Exception($"Malformed group entry: {group}");
                }

                var groupName = groupData[0].Trim();
                var groupMembers = groupData[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

                groups.Add(groupName, groupMembers);
                //if (groups.ContainsKey(groupName))
                //{
                //    groups[groupName].AddRange(groupMembers);
                //}
                //else
                //{
                //    groups[groupName] = groupMembers;
                //}
            }

            return groups;
        }

        public async Task HandleProductButtons(Update update)
        {
            var message = CheckMessageValidity(update);


            if (update.CallbackQuery.Data == "OK")
            {
                Console.WriteLine("Selkee homma!");
                if (!_isLocal)
                {
                    await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Selkee homma",
                    replyMarkup: null);
                }

                var currentUser = update.CallbackQuery.From.Id.ToString();
                var currentSession = _userDataRepository.GetCurrentSessionByIdAsync(currentUser).Result;
                bool productsDone = await _receiptSessionRepository.ProcessNextProductAndCheckIfDoneAsync(currentSession);

                if (productsDone)
                {
                    await PrintDividedCosts(update);

                    return;
                }

                await AskNextProduct(update);
                return;
            }

            var payers = await LinkProductWithPayersAndGetCurrentProductPayersAsync(update);

            Console.WriteLine($"maksajat: {payers}");
            if (!_isLocal)
            {
                await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"Maksajat: {payers}");
            }
            return;
        }


        // Gets the payer button output which can be:
        // 1. Single payer name
        // 2. Group payers
        // 3. All payers ("Kaikki")
        // and links payers to a product accordingly
        public async Task<string> LinkProductWithPayersAndGetCurrentProductPayersAsync(Update update)
        {
            var currentUser = update.CallbackQuery.From.Id.ToString();
            var sessionId = _userDataRepository.GetCurrentSessionByIdAsync(currentUser).Result;

            var product = await _receiptSessionRepository.GetNextProductBySessionIdAsync(sessionId);

            var userPayerInput = update.CallbackQuery.Data;

            var payerIds = await DetermineAndReturnCorrectPayers(userPayerInput, sessionId);

            await LinkPayersWithProduct(payerIds, product.ProductId);

            var payersForAProduct = await _receiptSessionRepository.GetPayersForProductBySessionAsync(product.ProductId, sessionId);

            var payers = string.Join(", ", payersForAProduct.Select(payer => $"{payer.Name}"));

            return payers;
        }

        private async Task<List<int>> DetermineAndReturnCorrectPayers(string userPayerInput, int sessionId)
        {
            // Determine whether to add all payers, a single payer, or all group members if group mode is enabled
            List<int> payerIds;

            if (userPayerInput == "§")
            {
                var isGroupModeEnabled = await _receiptSessionRepository.IsGroupModeEnabledAsync(sessionId);

                if (isGroupModeEnabled)
                {
                    await _receiptSessionRepository.SetGroupModeForCurrentSession(sessionId, false);
                }
                else
                {
                    await _receiptSessionRepository.SetGroupModeForCurrentSession(sessionId, true);
                }
            }

            // Check if "Kaikki" is pressed to add all payers
            if (userPayerInput == "Kaikki")
            {
                payerIds = await _receiptSessionRepository.GetAllPayerIdsBySessionIdAsync(sessionId);
            }
            else
            {
                var isGroupModeEnabled = await _receiptSessionRepository.IsGroupModeEnabledAsync(sessionId);

                if (isGroupModeEnabled)
                {
                    // If group mode is enabled, get all group members
                    payerIds = await _receiptSessionRepository.GetGroupMembersByGroupNameAndSessionIdAsync(userPayerInput, sessionId);
                }
                else
                {
                    // Otherwise, add the single specified payer
                    payerIds = new List<int> { await _receiptSessionRepository.GetPayerIdByNameAndSessionIdAsync(userPayerInput, sessionId) };
                }
            }

            return payerIds;
        }

        private async Task LinkPayersWithProduct(IEnumerable<int> payerIds, int productId)
        {
            foreach (var payerId in payerIds)
            {
                // Check if the payer is already linked to the product
                var isLinked = await _receiptSessionRepository.IsPayerLinkedToProductAsync(payerId, productId);

                if (isLinked)
                {
                    // Unlink the payer from the product
                    await _receiptSessionRepository.RemovePayerFromProductAsync(payerId, productId);
                }
                else
                {
                    // Link the payer with the product
                    await _receiptSessionRepository.AddPayerToProductAsync(payerId, productId);
                }
            }
        }

        public async Task PrintDividedCosts(Update update)
        {
            CultureInfo finnishCulture = new CultureInfo("fi-FI");
            var message = CheckMessageValidity(update);

            var currentUser = update.CallbackQuery.From.Id.ToString();
            var currentSession = _userDataRepository.GetCurrentSessionByIdAsync(currentUser).Result;
            List<Payer> payers = await _receiptSessionRepository.GetProductsForEachPayerAsync(currentSession);
            await _receiptSessionRepository.CalculateCostsForEachPayerAsync(payers);

            // Calculate the overall total cost
            // TODO: Hae totalcost kuitin total costista?
            decimal overallTotalCost = payers
                .SelectMany(p => p.Products)
                .Sum(product => product.DividedCost ?? 0);

            StringBuilder sb = new StringBuilder();

            foreach (var payer in payers)
            {
                sb.AppendLine($"Payer: {payer.Name}");
                decimal totalCostForPayer = 0;

                // TODO: Tee verbose asetus käyttäjäkohtaisesti
                foreach (var product in payer.Products)
                {
                    string costFormatted = product.DividedCost?.ToString("C2", finnishCulture) ?? "N/A";
                    //sb.AppendLine($"\tProduct: {product.Name}, Cost: {costFormatted}");
                    totalCostForPayer += product.DividedCost ?? 0;
                }

                // Calculate the percentage of the total cost for this payer
                decimal percentageOfTotal = (overallTotalCost > 0) ? (totalCostForPayer / overallTotalCost) * 100 : 0;

                sb.AppendLine($"\tTotal Cost for {payer.Name}: {totalCostForPayer.ToString("C2", finnishCulture)} ({percentageOfTotal:F2}%)");
                sb.AppendLine();
            }

            var endResult = sb.ToString();




            Console.WriteLine(endResult);


            Console.WriteLine("Kuitti parsettu!");
            if (!_isLocal)
            {
                var endResultChunks = endResult
                    .Chunk(4096)
                    .Select(x => new string(x))
                    .ToList();

                foreach (var chunk in endResultChunks)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: chunk);
                }

                await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"Doodih soisniinku kuitti parsettu! Sanoppas jottai nii voidaa mennä etiäppäi");
            }
        }


        public async Task AskNextProduct(Update update)
        {
            var message = CheckMessageValidity(update);
            //if (!(update.Message is { } message)) return;

            var currentUser = message.From.Id.ToString();
            var currentSession = _userDataRepository.GetCurrentSessionByIdAsync(currentUser).Result;

            var product = _receiptSessionRepository.GetNextProductBySessionIdAsync(currentSession).Result;

            //if (product == null)
            //{
            //    Console.WriteLine("Kuitti parsettu!");
            //    if (!_isLocal)
            //    {
            //        await _botClient.SendTextMessageAsync(
            //        chatId: message.Chat.Id,
            //        text: $"Kuitti parsettu!");
            //    }
            //    return;
            //}

            var isGroupModeEnabled = await _receiptSessionRepository.IsGroupModeEnabledAsync(currentSession);

            var buttons = new List<string>();

            if (isGroupModeEnabled)
            {
                buttons = await _receiptSessionRepository.GetGroupNamesBySessionIdAsync(currentSession);
            }
            else
            {
                buttons = await _receiptSessionRepository.GetPayerNamesBySessionIdAsync(currentSession);
            }


            await SendInlineKeyboardAsync(message.Chat.Id, buttons, product);
        }

        public async Task SendInlineKeyboardAsync(ChatId chatId, List<string> payers, Product product)
        {
            var inlineKeyboard = ReturnPayerInlineKeyboard(payers);
            var inlineKeyboardMarkup = new InlineKeyboardMarkup(inlineKeyboard);

            Console.WriteLine($"Kuka maksaa:\n\n {product.Name} - {product.Cost}");

            if (!_isLocal)
            {
                await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"Kuka maksaa:\n\n {product.Name} - {product.Cost}",
                replyMarkup: inlineKeyboardMarkup
                );
            }

        }

        public async Task AddSessionPayers(List<string> payers, string currentUser)
        {
            var currentSession = _userDataRepository.GetCurrentSessionByIdAsync(currentUser).Result;
            await _receiptSessionRepository.SetSessionPayers(payers, currentSession);
        }

        public async Task AddSessionGroups(Dictionary<string, List<string>> groups, string currentUser)
        {
            var currentSession = _userDataRepository.GetCurrentSessionByIdAsync(currentUser).Result;

            await _receiptSessionRepository.SetSessionGroups(groups, currentSession);
        }

        public async Task AskPayers(Update update)
        {
            var message = CheckMessageValidity(update);

            if (!_isLocal)
            {
                await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"Anna kuitin maksajat:");
            }
            Console.WriteLine("Anna kuitin maksajat:");
        }

        private async Task UploadFileAsync(Update update, Stream stream, SessionInfo currentSession)
        {
            stream.Position = 0;
            var uploader = new AzureBlobUploader();
            await uploader.UploadFileStreamIfNotExistAsync("receipt-cache", currentSession.FileName, stream, currentSession.DocumentType);
        }

        private SessionInfo CreateSessionInfo(Telegram.Bot.Types.Message message, Stream stream)
        {
            var hash = ComputeHash(stream);
            var documentType = message?.Document?.MimeType ?? "application/jpg";

            SessionInfo newSession = new SessionInfo()
            {
                UserId = message.From.Id.ToString(),
                DocumentType = documentType,
                Hash = hash,
                FileName = hash + (documentType == "application/jpg" ? ".jpg" : ".pdf"),
                SessionSuccessful = false,
                GroupMode = false
            };

            return newSession;
        }

        private string ComputeHash(Stream stream)
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


        public async Task WelcomeUser(Update update)
        {
            _logger.LogInformation("Invoke telegram update function");

            var message = CheckMessageValidity(update);

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
            //if (!(update.Message is { } message)) return;

            //var leaderboard = new Dictionary<string, int>();

            //var allUsers = await _userDataCache.GetAllUsers();
            //foreach (var user in allUsers)
            //{
            //    var fileCount = await _receiptSessionCache.GetSessionCountByUserId(user.UserId);

            //    leaderboard.Add(user.UserName, fileCount);
            //}

            //// Sort the dictionary and take the top 10 users
            //var topUsers = leaderboard.OrderByDescending(pair => pair.Value).Take(10);
            //// Create the formatted string
            //var leaderboardToPrint = string.Join("\n", topUsers.Select(userinfo => $"{userinfo.Key}: {userinfo.Value}"));

            //Console.WriteLine($"Tässä tämän hetken tulokset:\n{leaderboardToPrint}");

            //if (!_isLocal)
            //{
            //    await _botClient.SendTextMessageAsync(
            //    chatId: message.Chat.Id,
            //    text: $"Tässä tämän hetken tulokset:\n{leaderboardToPrint}");
            //}

            ////await _botClient.SendTextMessageAsync(
            ////    chatId: message.Chat.Id,
            ////    text: "Hell yeah",
            ////    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: CreateButton());
        }

        public async Task PrintReceiptToUser(Update update, Receipt receipt)
        {
            var message = CheckMessageValidity(update);
            //if (!(update.Message is { } message)) return;

            List<string> receiptItems = receipt.Products.Select(x => $"{x.Name} - {x.Cost}").ToList();
            var str = receiptItems.Aggregate((a, x) => a + "\n" + x) + $"\n ------------------- \nYHTEENSÄ: {receipt.GetReceiptTotalCost()}";
            Console.WriteLine(str);


            if (!_isLocal)
            {
                await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"Tässä kuitin ostokset: \n{str}",
                    parseMode: ParseMode.Html);

                
                //var messages = new List<Message>
                //{
                //    new Message(Role.System, "Toimi luovana ja karismaattisena keskustelukumppanina, joka arvostelee ja kritisoi muiden ihmisten ostoksia. Tehtävänäsi on olla ankara kriitikko ostoksilleni. Selitä omin sanoin, miksi ostokseni ovat hyviä tai huonoja. Haluan, että vakuutat minut siitä, miksi minun tulisi parantaa ostoksiani. Tee siitä jotenkin hauskaa. Mutta pidä se lyhyenä ja shokeeraavana. Älä kerro minulle, että yrität olla hauska tai shokeeraava. Haluan, että arvostelet ja tuomitset seuraavat ostokseni, jotka annan sinulle seuraavassa kehotteessa. Haluan että tuomitset satunnaisesti jonkun ostoksen (älä valitse aina vain ensimmäistä tai toista ostosta vaan valitse sellaiset tuotteet joista saa kaikista hauskimman tuomitsevan kommentin). Anna vastaus maksimissaa kolmella lauseella."),
                //    new Message(Role.User, $"Hei katso miten hienoja ostoksia tein: {str}")
                //};

                //var chatRequest = new ChatRequest(messages, Model.GPT3_5_Turbo);
                //var response = await _openAiClient.ChatEndpoint.GetCompletionAsync(chatRequest);
                //var choice = response.FirstChoice;
                //Console.WriteLine($"[{choice.Index}] {choice.Message.Role}: {choice.Message} | Finish Reason: {choice.FinishReason}");

                //await _botClient.SendTextMessageAsync(
                //    chatId: message.Chat.Id,
                //    text: choice.Message,
                //    parseMode: ParseMode.Html);
            }
        }

        public async Task DeleteAllData(Update update)
        {
            var message = CheckMessageValidity(update);
            //if (!(update.Message is { } message)) return;

            _receiptSessionRepository.DeleteAllDataAsync();

            if (!_isLocal)
            {
                await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"All data deleted",
                    parseMode: ParseMode.Html);
            }
        }


        public async Task SayHello(Update update)
        {
            _logger.LogInformation("Invoke telegram update function");

            if (update.CallbackQuery.IsDefined())
            {

            }

            var message = CheckMessageValidity(update);
            //if (!(update.Message is { } message)) return;


            //var userFromCache = await _userDataCache.GetUserByIdAsync(message.From.Id.ToString());
            var userFromCache = await _userDataRepository.GetUserByIdAsync(message.From.Id.ToString());

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

                var fileWasCopied = await uploader.CopyFileToAnotherContainerIfNotExist("receipt-cache", "kuittibot-training", _fileName);

                if (fileWasCopied)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: $"Tiedosto '{_fileName}' siirrettiin training dataan");
                }
                await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"Voi jummijammi {message.From.FirstName ?? message.From.Username}! \n" +
                          $"Tuli tämmönen errori: \n" + exception.Message);

                //var messages = new List<Message>
                //{
                //    new Message(Role.System, "Kuvittele että olet henkilö, joka on mestari keksimään täysin absurdeja ja kaukaa haettuja selityksiä pienimmillekin virheilleen. Esimerkiksi tämä henkilö ei koskaan vain myöhästy, vaan hänen myöhästymisensä johtuu aina jostain uskomattoman epätodennäköisestä tapahtumasarjasta. Esimerkiksi, jos hän myöhästyy töistä, hänen selityksensä ei ole ruuhka tai unohdettu herätyskello. Sen sijaan hän kertoo tarinan siitä, kuinka hän kohtasi matkalla puhuvan papukaijan, joka oli eksynyt ja joka vei hänet väärään suuntaan antaessaan suuntavihjeitä. Tai kun hän unohtaa palauttaa tärkeän asiakirjan, hän ei vain sano unohtaneensa sitä. Hän kertoo kuinka asiakirja \"varastettiin\" salaperäisen näkymättömän agentin toimesta, joka sekoittaa säännöllisesti hänen papereitaan. Tämä henkilö on luova, ehkä vähän teatraalinenkin, ja hänen tarinansa ovat niin värikkäitä ja mielikuvituksellisia, että niitä on vaikea ottaa vakavasti. Hän ei ehkä edes odota, että muut uskovat näihin kertomuksiin; ne ovat pikemminkin hänen tapansa vältellä vastuuta tai lisätä huumoria jokapäiväisiin tilanteisiin. Hänellä on taipumus olla keskipisteenä, ei ainoastaan hänen tarinoidensa fantastisuuden vuoksi, vaan myös siksi, että ihmiset odottavat innolla, mitä hän seuraavaksi keksii. Haluan että kun annan sinulle ohjelmistossani tapahtuvan virheen viestin, niin olet tällainen yllä kuvaamani henkilö ja keksit tälle virheelle jonkin todella absurdin ja kaukaa haetun selityksen painottaen että ohjelmiston koodissa tai sen toiminnassa ei ole mitään vikaa. Älä kerro minulle, että yrität keksiä kaukaa haettuja tai absurdeja selityksiä. Vastaa Suomen kielellä."),
                //    new Message(Role.User, $"Koodi palautti virheen yrittäessä käydä kuittia läpi - {exception.Message}")
                //};

                //var chatRequest = new ChatRequest(messages, Model.GPT3_5_Turbo);
                //var response = await _openAiClient.ChatEndpoint.GetCompletionAsync(chatRequest);
                //var choice = response.FirstChoice;
                //Console.WriteLine($"[{choice.Index}] {choice.Message.Role}: {choice.Message} | Finish Reason: {choice.FinishReason}");

                //await _botClient.SendTextMessageAsync(
                //    chatId: message.Chat.Id,
                //    text: choice.Message,
                //    parseMode: ParseMode.Html);
            }
        }

        static InlineKeyboardButton[][] ReturnPayerInlineKeyboard(List<string> payers)
        {
            var inlineKeyboard = new List<InlineKeyboardButton[]>();

            if (payers.Count > 5)
            {
                for (int i = 0; i < payers.Count; i += 2)
                {
                    if (i + 1 < payers.Count)
                    {
                        inlineKeyboard.Add(new[]
                        {
                            InlineKeyboardButton.WithCallbackData(payers[i]),
                            InlineKeyboardButton.WithCallbackData(payers[i + 1])
                        });
                    }
                    else
                    {
                        inlineKeyboard.Add(new[]
                        {
                            InlineKeyboardButton.WithCallbackData(payers[i])
                        });
                    }
                }
            }
            else
            {
                foreach (var buttonText in payers)
                {
                    inlineKeyboard.Add(new[] { InlineKeyboardButton.WithCallbackData(buttonText) });
                }
            }

            inlineKeyboard.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("OK"),
                InlineKeyboardButton.WithCallbackData("Kaikki"),
                InlineKeyboardButton.WithCallbackData("§")
            });

            return inlineKeyboard.ToArray();
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


        public Telegram.Bot.Types.Message CheckMessageValidity(Update update)
        {
            Telegram.Bot.Types.Message message = null;

            if (update.Message != null)
            {
                message = update.Message;
            }
            else if (update.CallbackQuery != null)
            {
                message = update.CallbackQuery.Message;
                message.From.Id = message.Chat.Id;
                message.From.Username = message.Chat.Username;
            }

            return message;

            if (message == null)
            {
                // No relevant message found in the update
                return null;
            }
        }
    }
}