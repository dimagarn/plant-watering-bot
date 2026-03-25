using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Configuration;
using Hangfire;
using Hangfire.PostgreSql;
using TelegramQuestBot.Models;

public class BotHandler
{
    private readonly AppDbContext _dbContext;
    private static TelegramBotClient _bot = null!;
    private readonly PlantService plantService;

    public BotHandler(AppDbContext dbContext, TelegramBotClient bot)
    {
        _dbContext = dbContext;
        _bot = bot;
        plantService = new PlantService(_dbContext, _bot);
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message && update.Message?.Text == "/start")
        {
            await _bot.SendMessage(
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
        else if (update.Type == UpdateType.Message && update.Message?.Text?.StartsWith("/settime") == true)
            await plantService.SetTime(update.Message);
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Произошла ошибка: {exception.Message}");
        return Task.CompletedTask;
    }
}