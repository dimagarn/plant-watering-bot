using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramQuestBot.Models;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using System.Text;


public class PlantService
{
    private readonly AppDbContext _dbContext;
    private static TelegramBotClient _bot = null!;
    public PlantService(AppDbContext dbContext, TelegramBotClient bot)
    {
        _dbContext = dbContext;
        _bot = bot;
    }

    public async Task CreatePlant(Message message, CancellationToken cancellationToken)
    {
        var args = message?.Text?.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (args.Length != 3)
        {
            await _bot.SendMessage(message.Chat.Id, "Правильный формат: /createplant <название> <частота>");
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

        try
        {
            _dbContext.Plants.Add(plant);
            await _dbContext.SaveChangesAsync();
            RecurringJob.AddOrUpdate(GetJobId(plant.Id, plant.ChatId), () => SendWateringReminder(plant.Id), GetCronExpression(plant));
            await _bot.SendMessage(
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
            await _bot.SendMessage(message.Chat.Id, "❌ Ошибка при добавлении растения. Проверьте данные и попробуйте снова.");
        }
    }

    public async Task MyPlants(long chatid)
    {
        var plantsList = await _dbContext.Plants.Where(p => p.ChatId == chatid).ToListAsync();
        var stringBuilder = new StringBuilder();

        if (plantsList.Any())
            foreach (var plant in plantsList)
                stringBuilder.AppendLine(
                    $"🌱 {plant.Name} [#{plant.Id}]\n 💧 Полив: {plant.WateringFrequency}\n 📅 Следующий полив: {plant.NextWateringDate.ToShortDateString()}\n ⏰ Напоминание: {plant.NotificationHour}:00 \n");
        else
        {
            await _bot.SendMessage(chatid, "Похоже, что у тебя еще нет растений, не беда! Используй команду /createplant");
            return;
        }

        await _bot.SendMessage(chatid, stringBuilder.ToString());
    }

    public async Task DeletePlant(Message message)
    {
        var chatId = message.Chat.Id;
        var plantId = new int();

        if (int.TryParse(message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1], out int plantid))
        {
            plantId = plantid;
        }
        else
        {
            await _bot.SendMessage(chatId, "ошибка формата!");
            return;
        }
        var plant = await _dbContext.Plants.FindAsync(plantid);
        if (plant != null && plant.ChatId == chatId)
        {
            _dbContext.Plants.Remove(plant);
            await _dbContext.SaveChangesAsync();
            RecurringJob.RemoveIfExists(GetJobId(plantId, chatId));
            await _bot.SendMessage(chatId, $"✅ Растение с id: {plantid} успешно удалено!");
        }
        else
        {
            await _bot.SendMessage(chatId, $"❌ Растение с id: {plantid} не найдено!");
            return;
        }
    }

    public async Task SetTime(Message message)
    {
        var chatId = message.Chat.Id;
        var plantId = new int();
        var notificationHour = new byte();
        var args = message?.Text?.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (int.TryParse(args[1], out int plantid) && byte.TryParse(args[2], out byte notificationhour))
        {
            plantId = plantid;
            notificationHour = notificationhour;
        }
        else
        {
            await _bot.SendMessage(chatId, "ошибка формата!");
            return;
        }
        var plant = await _dbContext.Plants.FindAsync(plantid);
        if (plant != null && plant.ChatId == chatId)
        {
            plant.NotificationHour = notificationHour;
            await _dbContext.SaveChangesAsync();
            RecurringJob.AddOrUpdate(GetJobId(plant.Id, plant.ChatId), () => SendWateringReminder(plant.Id), GetCronExpression(plant));
            await _bot.SendMessage(chatId, $"✅ Для Растения с id: {plantid} успешно установлено время напоминания: {notificationHour}:00!");
        }
        else
        {
            await _bot.SendMessage(chatId, $"❌ Растение с id: {plantid} не найдено!");
            return;
        }
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
        plant.NextWateringDate = DateTime.UtcNow.AddDays(plant.WateringFrequency.ToLower() switch
        {
            "ежедневно" => 1,
            "еженедельно" => 7,
            "ежемесячно" => 30,
            _ => throw new ArgumentException("Invalid watering frequency")
        });
        await dbContext.SaveChangesAsync();
        await _bot.SendMessage(plant.ChatId, $"Напоминание! Полить растение: {plant.Name} 🌿");
    }

    static string GetJobId(int plantId, long chatId) =>
$"Remind for plant: {plantId}, chat: {chatId}";

    static string GetCronExpression(Plant plant) =>
plant.WateringFrequency.ToLower() switch
{
    "ежедневно" => $"0 {plant.NotificationHour} * * *",
    "еженедельно" => $"0 {plant.NotificationHour} * * {(int)DateTime.UtcNow.DayOfWeek}",
    "ежемесячно" => $"0 {plant.NotificationHour} * {DateTime.UtcNow.Day} *",
    _ => throw new ArgumentException("Invalid watering frequency")
};
}