using Telegram.Bot;
using Telegram.Bot.Polling;
using Microsoft.Extensions.Configuration;
using Hangfire;
using Hangfire.PostgreSql;

namespace TelegramQuestBot
{
    class Program
    {
        static TelegramBotClient bot;
        static async Task Main()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            var token = config["BotConfiguration:token"];
            using var cts = new CancellationTokenSource();
            bot = new TelegramBotClient(token, cancellationToken: cts.Token);

            using var dbContext = new AppDbContext();
            await dbContext.Database.EnsureCreatedAsync();

            GlobalConfiguration.Configuration
                .UsePostgreSqlStorage(c =>
                c.UseNpgsqlConnection(config["ConnectionStrings:hangfireConnectionString"]));

            var server = new BackgroundJobServer();

            var botHandler = new BotHandler(dbContext, bot);
            var receiverOptions = new ReceiverOptions { AllowedUpdates = { } };
            bot.StartReceiving(
                updateHandler: botHandler.HandleUpdateAsync,
                errorHandler: botHandler.HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );

            Console.WriteLine("Бот запущен... Для завершения нажмите Ctrl+C.");
            await Task.Delay(-1, cts.Token);
        }
    }
}