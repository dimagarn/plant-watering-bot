using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Args;
using TelegramQuestBot.Models;
using Microsoft.EntityFrameworkCore;

namespace TelegramQuestBot
{
    class Program
    {
        static async Task Main()
        {
            var token = "7027999847:AAGvr5o-FUpIh5awURFezZyrVx50uhTSdBA";
            using var cts = new CancellationTokenSource();
            var bot = new TelegramBotClient(token, cancellationToken: cts.Token);

            // Инициализация базы данных (убедитесь, что она существует)
            using var dbContext = new AppDbContext();
            await dbContext.Database.EnsureCreatedAsync();  // Создаёт базу, если её нет (для простых случаев; для продакшена используйте миграции)

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
    }
}