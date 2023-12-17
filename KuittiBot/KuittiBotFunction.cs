using Azure.Core;
using Azure.Storage.Blobs;
using KuittiBot.Functions.Domain.Abstractions;
using KuittiBot.Functions.Domain.Models;
using KuittiBot.Functions.Services;
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
        private readonly IUserDataCache _userDataCache;
        private readonly IBotStateMachine _stateMachine;
        private static bool _isLocal = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));

        public KuittiBotFunction(UpdateService updateService, IUserDataCache userDataCache, IBotStateMachine stateMachine)
        {
            _updateService = updateService;
            _userDataCache = userDataCache;
            _stateMachine = stateMachine;
        }


        [FunctionName("KuittiotLocalTest")]
        public async Task Run([TimerTrigger("0 * */5 * * *", RunOnStartup = true)] TimerInfo timer)
        {

            if (_isLocal)
            {
                var body = "{\r\n    \"update_id\": 56781888,\r\n    \"message\":\r\n    {\r\n        \"message_id\": 395,\r\n        \"from\":\r\n        {\r\n            \"id\": 34155101,\r\n            \"is_bot\": false,\r\n            \"first_name\": \"Tommi\",\r\n            \"last_name\": \"Mikkola\",\r\n            \"username\": \"mibbbo\",\r\n            \"language_code\": \"fi\"\r\n        },\r\n        \"chat\":\r\n        {\r\n            \"id\": 34155101,\r\n            \"first_name\": \"Tommi\",\r\n            \"last_name\": \"Mikkola\",\r\n            \"username\": \"mibbbo\",\r\n            \"type\": \"private\"\r\n        },\r\n        \"date\": 1700071501,\r\n        \"photo\": [\r\n            {\r\n                \"file_id\": \"AgACAgQAAxkBAAIBi2VVCE2NZ0-QE010GFogje28NQb4AAKaujEbxImoUp56pG-xhvj4AQADAgADcwADMwQ\",\r\n                \"file_unique_id\": \"AQADmroxG8SJqFJ4\",\r\n                \"file_size\": 1033,\r\n                \"width\": 51,\r\n                \"height\": 90\r\n            },\r\n            {\r\n                \"file_id\": \"AgACAgQAAxkBAAIBi2VVCE2NZ0-QE010GFogje28NQb4AAKaujEbxImoUp56pG-xhvj4AQADAgADbQADMwQ\",\r\n                \"file_unique_id\": \"AQADmroxG8SJqFJy\",\r\n                \"file_size\": 17162,\r\n                \"width\": 180,\r\n                \"height\": 320\r\n            },\r\n            {\r\n                \"file_id\": \"AgACAgQAAxkBAAIBi2VVCE2NZ0-QE010GFogje28NQb4AAKaujEbxImoUp56pG-xhvj4AQADAgADeAADMwQ\",\r\n                \"file_unique_id\": \"AQADmroxG8SJqFJ9\",\r\n                \"file_size\": 76222,\r\n                \"width\": 450,\r\n                \"height\": 800\r\n            },\r\n            {\r\n                \"file_id\": \"AgACAgQAAxkBAAIBi2VVCE2NZ0-QE010GFogje28NQb4AAKaujEbxImoUp56pG-xhvj4AQADAgADeQADMwQ\",\r\n                \"file_unique_id\": \"AQADmroxG8SJqFJ-\",\r\n                \"file_size\": 136631,\r\n                \"width\": 720,\r\n                \"height\": 1280\r\n            }\r\n        ]\r\n    }\r\n}";
                var update = JsonConvert.DeserializeObject<Update>(body);
                try
                {
                    if (update != null)
                    {
                        await _stateMachine.OnUpdate(update);
                    }

                }
#pragma warning disable CA1031
                catch (Exception e)
#pragma warning restore CA1031
                {
                    await _updateService.LogError(update, e);
                }
            }
        }


        [FunctionName("KuittiBot")]
        public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]
        HttpRequest request,
        ILogger logger
        /*, CancellationToken token*/)
        {
            //using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(token, request.HttpContext.RequestAborted);
            var body = await request.ReadAsStringAsync();
            var update = JsonConvert.DeserializeObject<Update>(body); 
            try
            {
                if (update != null)
                {
                    await _stateMachine.OnUpdate(update);
                }

                return new OkResult();
            }
#pragma warning disable CA1031
            catch (Exception e)
#pragma warning restore CA1031
            {
                logger.LogError("Exception: " + e.Message);
                await _updateService.LogError(update, e);
            }

            return new OkResult();
        }
    }
}

