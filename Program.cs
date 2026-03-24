using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
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

            var plantService = new PlantService(dbContext, bot);

            GlobalConfiguration.Configuration
                .UsePostgreSqlStorage(c =>
                c.UseNpgsqlConnection(config["ConnectionStrings:hangfireConnectionString"]));

            var server = new BackgroundJobServer();

            async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
            {
                if (update.Type == UpdateType.Message && update.Message?.Text == "/start")
                {
                    await bot.SendMessage(
                        chatId: update.Message.Chat.Id,
                        text: "🌱 Привет! Давай знакомиться, Я - твой новый универсальный помощник, который будет напоминать тебе как и когда поливать твои растения, чтобы они всегда оставались здоровыми и красивыми!",
                        cancellationToken: cancellationToken);
                }
                else if (update.Type == UpdateType.Message && update.Message?.Text?.StartsWith("/createplant") == true)
                    await plantService.CreatePlant(update.Message, cancellationToken);
                else if (update.Type == UpdateType.Message && update.Message?.Text?.StartsWith("/myplants") == true)
                    await plantService.MyPlants(update.Message.Chat.Id);
                else if (update.Type == UpdateType.Message && update.Message?.Text?.StartsWith("/deleteplant") == true)
                    await plantService.DeletePlant(update.Message);
            }

            Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Произошла ошибка: {exception.Message}");
                return Task.CompletedTask;
            }

            var receiverOptions = new ReceiverOptions { AllowedUpdates = { } };
            bot.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );

            Console.WriteLine("Бот запущен... Для завершения нажмите Ctrl+C.");
            await Task.Delay(-1, cts.Token);
        }
    }
}