using AzureTableDataStore;
using Dapper;
using KuittiBot.Functions.Domain.Abstractions;
using KuittiBot.Functions.Domain.Models;
using KuittiBot.Functions.Infrastructure;
using KuittiBot.Functions.Services;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics.Metrics;
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
            builder.Services.AddSingleton<UpdateService>();

            builder.Services.AddSingleton<IReceiptParsingService, ReceiptParsingService>();

            // Dummy business-logic service
            builder.Services.AddSingleton<BotStateMachine>();
            builder.Services.AddSingleton<IBotStateMachine>(provider => provider.GetRequiredService<BotStateMachine>());

            // UserDataCache
            //builder.Services.AddSingleton<IUserDataCache, UserDataCache>();
            builder.Services.AddSingleton<ITableDataStore<UserDataEntity>, TableDataStore<UserDataEntity>>(sp =>
                new TableDataStore<UserDataEntity>(
                Environment.GetEnvironmentVariable("AzureWebJobsStorage", EnvironmentVariableTarget.Process),
                "userdatacache",
                true,
                "userdatacache",
                true,
                Azure.Storage.Blobs.Models.PublicAccessType.None,
                partitionKeyProperty: nameof(UserDataEntity.UserId),
                rowKeyProperty: nameof(UserDataEntity.UserName))
            );

            // ReceiptSessionCache
            builder.Services.AddSingleton<IReceiptSessionCache, ReceiptSessionCache>();
            builder.Services.AddSingleton<ITableDataStore<ReceiptSessionEntity>, TableDataStore<ReceiptSessionEntity>>(sp =>
                new TableDataStore<ReceiptSessionEntity>(
                Environment.GetEnvironmentVariable("AzureWebJobsStorage", EnvironmentVariableTarget.Process),
                "usersessioncache",
                true,
                "usersessioncache",
                true,
                Azure.Storage.Blobs.Models.PublicAccessType.None,
                partitionKeyProperty: nameof(ReceiptSessionEntity.UserId),
                rowKeyProperty: nameof(ReceiptSessionEntity.Hash))
            );

            // SQL UserDataRepository
            builder.Services.AddTransient<IUserDataRepository, UserDataRepository>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<UserDataRepository>>();
                var connectionString = Environment.GetEnvironmentVariable("KuittibotSqlConnectionString", EnvironmentVariableTarget.Process);
                return new UserDataRepository(logger, connectionString);
            });

            builder.Services.AddTransient<IReceiptSessionRepository, ReceiptSessionRepository>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<ReceiptSessionRepository>>();
                var connectionString = Environment.GetEnvironmentVariable("KuittibotSqlConnectionString", EnvironmentVariableTarget.Process);
                return new ReceiptSessionRepository(logger, connectionString);
            });
        }
    }
}