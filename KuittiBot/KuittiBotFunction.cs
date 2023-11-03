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

        public KuittiBotFunction(UpdateService updateService, IUserDataCache userDataCache, IBotStateMachine stateMachine)
        {
            _updateService = updateService;
            _userDataCache = userDataCache;
            _stateMachine = stateMachine;
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
                await _updateService.WelcomeUser(update);

                return new OkResult();

                // Old implementation


                if (update is null)
                {
                    logger.LogWarning("Unable to deserialize Update object.");
                    return new OkResult();
                }

                if (update.Message.Type == MessageType.Document && update.Message.Document.MimeType == "application/pdf")
                {
                    await _updateService.InitializeParseingForUser(update);
                }

                var userId = update.Message.From.Id.ToString() /*?? update.CallbackQuery.From.Id.ToString()*/;

                // get the latest fetch date time (previous fetch operation)
                var userFromCache = await _userDataCache.GetUserById(userId);

                if (userFromCache == null)
                {
                    var newUser = new UserDataCacheEntity()
                    {
                        Id = userId,
                        FileName = "",
                        UserName = update.Message.From.Username,
                        Description = "",
                        //Payers = new List<string>()
                    };

                    await _userDataCache.InsertAsync(newUser);
                    logger.LogInformation($"Inserted user '{newUser.UserName}'");
                        userFromCache = newUser;
                    await _updateService.WelcomeUser(update);
                    return new OkResult();
                };
                await _updateService.WelcomeUser(update);

                //if (!update.Message)



                //if (!(update.CallbackQuery is { } callbackQuery))
                //{
                //    await _updateService.EchoAsync(update);
                //};

                await _updateService.SayHello(update);



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

