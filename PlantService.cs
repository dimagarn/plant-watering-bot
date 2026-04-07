using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramQuestBot.Models;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using System.Text;
using Telegram.Bot.Types.ReplyMarkups;


public class PlantService
{
    private readonly AppDbContext _dbContext;
    private static TelegramBotClient _bot = null!;
    public PlantService(AppDbContext dbContext, TelegramBotClient bot)
    {
        _dbContext = dbContext;
        _bot = bot;
    }

    public async Task CreatePlant(long chatId, UserPlantData data, CancellationToken cancellationToken)
    {
        var name = data.PlantName;
        var wateringFrequency = data.WateringFrequency;

        var plant = new Plant
        {
            ChatId = chatId,
            Name = name,
            WateringFrequency = wateringFrequency,
            NextWateringDate = DateTime.UtcNow.AddDays(wateringFrequency switch
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
                chatId: chatId,
                text: $"✅ Растение '{plant.Name}' добавлено! Я буду напоминать тебе поливать его {plant.WateringFrequency} в {plant.NotificationHour}:00.",
                cancellationToken: cancellationToken,
                replyMarkup: Keyboards.Main);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при сохранении растения: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Внутренняя ошибка: {ex.InnerException.Message}");
            }
            await _bot.SendMessage(chatId, "❌ Ошибка при добавлении растения. Проверьте данные и попробуйте снова.");
        }
    }

    public async Task MyPlants(long chatId)
    {
        var plantsList = await _dbContext.Plants
        .Where(p => p.ChatId == chatId)
        .OrderBy(p => p.Id)
        .ToListAsync();

        if (!plantsList.Any())
        {
            await _bot.SendMessage(chatId, "Похоже, что у тебя еще нет растений, не беда! Используй кнопку 🌱 Добавить растение");
            return;
        }
        foreach (var plant in plantsList)
            await _bot.SendMessage(
                chatId,
                $"🌱 {plant.Name}\n 💧 Полив: {plant.WateringFrequency}\n 📅 Следующий полив: {plant.NextWateringDate.ToShortDateString()}\n ⏰ Напоминание: {plant.NotificationHour}:00 \n",
                replyMarkup: Keyboards.PlantActions(plant.Id));
    }

    public async Task DeletePlant(long chatId, int plantId, CancellationToken cancellationToken)
    {
        var plant = await _dbContext.Plants.FindAsync(plantId);
        if (plant is null || plant.ChatId != chatId) return;
        _dbContext.Plants.Remove(plant);
        await _dbContext.SaveChangesAsync();
        RecurringJob.RemoveIfExists(GetJobId(plantId, chatId));
        await _bot.SendMessage(chatId, $"✅ Растение: {plant.Name} успешно удалено!",
            cancellationToken: cancellationToken,
            replyMarkup: Keyboards.Main);
    }

    public async Task SetTime(long chatId, int plantId, byte notificationHour, CancellationToken cancellationToken)
    {
        var plant = await _dbContext.Plants.FindAsync(plantId);
        if (plant == null || plant.ChatId != chatId) return;
        plant.NotificationHour = notificationHour;
        await _dbContext.SaveChangesAsync();
        RecurringJob.AddOrUpdate(GetJobId(plant.Id, plant.ChatId), () => SendWateringReminder(plant.Id), GetCronExpression(plant));
        await _bot.SendMessage(chatId, $"✅ Для растения: {plant.Name} успешно установлено время напоминания: {notificationHour}:00!",
            cancellationToken: cancellationToken,
            replyMarkup: Keyboards.Main);
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
        plant.NextWateringDate = DateTime.UtcNow.AddDays(plant.WateringFrequency switch
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
plant.WateringFrequency switch
{
    "ежедневно" => $"0 {plant.NotificationHour} * * *",
    "еженедельно" => $"0 {plant.NotificationHour} * * {(int)DateTime.UtcNow.DayOfWeek}",
    "ежемесячно" => $"0 {plant.NotificationHour} {DateTime.UtcNow.Day} * *",
    _ => throw new ArgumentException("Invalid watering frequency")
};
}