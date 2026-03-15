using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramQuestBot.Models;
using Microsoft.EntityFrameworkCore;
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
                {
                    await CreatePlant(update.Message, cancellationToken);
                }
            }

            async Task CreatePlant(Message message, CancellationToken cancellationToken)
            {
                var args = message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (args.Length != 3)
                {
                    await bot.SendMessage(message.Chat.Id, "Правильный формат: /createplant <название> <частота>");
                    return;
                }
                var name = args[1];
                var wateringFrequency = args[2];

                var plant = new Plant
                {
                    ChatId = message.Chat.Id,
                    Name = name,
                    WateringFrequency = wateringFrequency,
                    NextWateringDate = DateTime.UtcNow.AddDays(wateringFrequency.ToLower() switch
                    {
                        "ежедневно" => 1,
                        "еженедельно" => 7,
                        "ежемесячно" => 30,
                        _ => throw new ArgumentException("Invalid watering frequency")
                    })
                };

                using var dbContext = new AppDbContext();
                try
                {
                    dbContext.Plants.Add(plant);
                    await dbContext.SaveChangesAsync();
                    RecurringJob.AddOrUpdate($"Remind for plant: {plant.Id}, chat: {plant.ChatId}", () => SendWateringReminder(plant.Id), plant.WateringFrequency.ToLower() switch
                    {
                        "ежедневно" => Cron.Daily,
                        "еженедельно" => Cron.Weekly,
                        "ежемесячно" => Cron.Monthly,
                        _ => throw new ArgumentException("Invalid watering frequency")});
                    await bot.SendMessage(
                        chatId: message.Chat.Id,
                        text: $"✅ Растение '{plant.Name}' добавлено! Я буду напоминать тебе поливать его {plant.WateringFrequency}.",
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при сохранении растения: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Внутренняя ошибка: {ex.InnerException.Message}");
                    }
                    await bot.SendMessage(message.Chat.Id, "❌ Ошибка при добавлении растения. Проверьте данные и попробуйте снова.");
                }
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

        public static async Task SendWateringReminder(int plantid)
            {
                using var dbContext = new AppDbContext();
                var plant = await dbContext.Plants.FindAsync(plantid);
                if (plant is null)
                {
                    Console.WriteLine("Ошибка, растение не найдено!");
                    return;
                }
                await bot.SendMessage(plant.ChatId, $"Напоминание! Полить растение: {plant.Name} 🌿");
            }
    }
}