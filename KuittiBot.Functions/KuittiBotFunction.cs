using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace KuittiBot.Functions
{

    public class KuittiBotFunction
    {
        private readonly UpdateService _updateService;

        public KuittiBotFunction(UpdateService updateService)
        {
            _updateService = updateService;
        }

        [FunctionName("KuittiBot")]
        public async Task<IActionResult> Update(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]
        HttpRequest request,
            ILogger logger)
        {
            try
            {
                var body = await request.ReadAsStringAsync();
                var update = JsonConvert.DeserializeObject<Update>(body);
                if (update is null)
                {
                    logger.LogWarning("Unable to deserialize Update object.");
                    return new OkResult();
                }

                await _updateService.EchoAsync(update);
            }
#pragma warning disable CA1031
            catch (Exception e)
#pragma warning restore CA1031
            {
                logger.LogError("Exception: " + e.Message);
            }

            return new OkResult();
        }
    }

    //public static class TelegramBotFunction
    //{
    //    //private static readonly TelegramBotClient _botClient;

    //    //public SetUpBot()
    //    //{
    //    //    _botClient = new TelegramBotClient(System.Environment.GetEnvironmentVariable("TelegramBotToken", EnvironmentVariableTarget.Process));
    //    //}

    //    [FunctionName("Function1")]
    //    public static async Task<IActionResult> Run(
    //        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
    //        ILogger log)
    //    {
    //        log.LogInformation("C# HTTP trigger function processed a request.");

    //        string name = req.Query["name"];

    //        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    //        dynamic data = JsonConvert.DeserializeObject(requestBody);
    //        name = name ?? data?.name;

    //        string responseMessage = string.IsNullOrEmpty(name)
    //            ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
    //            : $"Hello, {name}. This HTTP triggered function executed successfully.";

    //        return new OkObjectResult(responseMessage);
    //    }

    //    //private const string SetUpFunctionName = "setup";
    //    //private const string UpdateFunctionName = "handleupdate";

    //    //[FunctionName(SetUpFunctionName)]
    //    //public static async Task RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
    //    //{
    //    //    var handleUpdateFunctionUrl = req.GetDisplayUrl.ToString().Replace(SetUpFunctionName, UpdateFunctionName,
    //    //    ignoreCase: true, culture: CultureInfo.InvariantCulture);
    //    //    await _botClient.SetWebhookAsync(handleUpdateFunctionUrl);
    //    //}

    //    //[FunctionName(UpdateFunctionName)]
    //    //public static async Task Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    //    //{
    //    //    var request = await req.ReadAsStringAsync();
    //    //    var update = JsonConvert.DeserializeObject<Telegram.Bot.Types.Update>(request);

    //    //    if (update.Type != UpdateType.Message)
    //    //        return;
    //    //    if (update.Message!.Type != MessageType.Text)
    //    //        return;

    //    //    await _botClient.SendTextMessageAsync(
    //    //    chatId: update.Message.Chat.Id,
    //    //    text: GetBotResponseForInput(update.Message.Text));
    //    //}

    //    //private string GetBotResponseForInput(string text)
    //    //{
    //    //    try
    //    //    {
    //    //        if (text.Contains("pod bay doors", StringComparison.InvariantCultureIgnoreCase))
    //    //        {
    //    //            return "I'm sorry Dave, I'm afraid I can't do that";

    //    //        }

    //    //        return new DataTable().Compute(text, null).ToString();
    //    //    }
    //    //    catch
    //    //    {
    //    //        return $"Dear human, I can solve math for you, try '2 + ";

    //    //    }
    //    //}
    //}
}

