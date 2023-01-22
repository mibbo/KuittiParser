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
}

