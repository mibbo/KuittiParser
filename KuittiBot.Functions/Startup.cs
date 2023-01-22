using AzureTableDataStore;
using KuittiBot.Functions;
using KuittiBot.Functions.Domain.Abstractions;
using KuittiBot.Functions.Domain.Models;
using KuittiBot.Functions.Infrastructure;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using Telegram.Bot;

[assembly: FunctionsStartup(typeof(KuittiBot.Functions.Startup))]

namespace KuittiBot.Functions
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            // Register ILogger<T> and ILoggerFactory
            builder.Services.AddLogging();

            var token = Environment
                .GetEnvironmentVariable("tgtoken", EnvironmentVariableTarget.Process)
                ?? throw new ArgumentException("Can not get token. Set token in environment setting");

            // Register named HttpClient to get benefits of IHttpClientFactory
            // and consume it with ITelegramBotClient typed client.
            // More read:
            //  https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-5.0#typed-clients
            //  https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests
            builder.Services.AddHttpClient("tgclient")
                .AddTypedClient<ITelegramBotClient>(httpClient
                    => new TelegramBotClient(token, httpClient));

            // Dummy business-logic service
            builder.Services.AddScoped<UpdateService>();

            // Tablestorage
            builder.Services.AddSingleton<IUserDataCache, UserDataCache>();
            builder.Services.AddSingleton<ITableDataStore<UserDataCacheEntity>, TableDataStore<UserDataCacheEntity>>(sp =>
                new TableDataStore<UserDataCacheEntity>(
                Environment.GetEnvironmentVariable("AzureWebJobsStorage", EnvironmentVariableTarget.Process),
                "userdatacache",
                true,
                "userdatacache",
                true,
                Azure.Storage.Blobs.Models.PublicAccessType.None,
                partitionKeyProperty: nameof(UserDataCacheEntity.Id),
                rowKeyProperty: nameof(UserDataCacheEntity.FileName))
            );

            // TODO blobstorage
            //builder.Services.AddTransient<IStorage<SendPurchaseOrders.RecipeStorage>,
            //    BlobStorage<SendPurchaseOrders.RecipeStorage>>());
            //builder.Services.Configure<BlobStorageConfiguration<SendPurchaseOrders.ArchiveStorage>>(config =>
            //{
            //    config.ConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage", EnvironmentVariableTarget.Process);
            //    config.ContainerName = "recipecache";
            //    config.CreateContainer = true;
            //});
        }
    }
}