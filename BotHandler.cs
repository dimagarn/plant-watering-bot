using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Collections.Concurrent;

public class BotHandler
{
    private readonly AppDbContext _dbContext;
    private static TelegramBotClient _bot = null!;
    private readonly PlantService _plantService;
    private readonly ConcurrentDictionary<long, UserState> _userStates = new();
    private readonly ConcurrentDictionary<long, UserPlantData> _userPlantData = new();

    public BotHandler(AppDbContext dbContext, TelegramBotClient bot)
    {
        _dbContext = dbContext;
        _bot = bot;
        _plantService = new PlantService(_dbContext, _bot);
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        UserState state;
        if (update.Message?.Chat.Id is null || update.Type != UpdateType.Message) return;
        var chatId = update.Message.Chat.Id;
        _userStates.TryAdd(chatId, UserState.Idle);
        if (update.Message?.Text == "/start")
        {
            await _bot.SendMessage(
                chatId: chatId,
                text: "🌱 Привет! Давай знакомиться, Я - твой новый универсальный помощник, который будет напоминать тебе как и когда поливать твои растения, чтобы они всегда оставались здоровыми и красивыми!",
                cancellationToken: cancellationToken,
                replyMarkup: Keyboards.Main);
        }
        else if (update.Message?.Text?.StartsWith("/createplant") == true)
            await _plantService.CreatePlant(update.Message, cancellationToken);
        else if ((update.Message?.Text?.StartsWith("/myplants") == true || update.Message?.Text?.StartsWith("📋 Мои растения") == true))
            await _plantService.MyPlants(chatId);
        else if (update.Message?.Text?.StartsWith("/deleteplant") == true)
            await _plantService.DeletePlant(update.Message, cancellationToken);
        else if (update.Message?.Text?.StartsWith("/settime") == true)
            await _plantService.SetTime(update.Message);
        else if (update.Message?.Text?.StartsWith("🌱 Добавить растение") == true)
        {
            _userStates[chatId] = UserState.WaitingForPlantName;
            _userPlantData[chatId] = new UserPlantData();
            await _bot.SendMessage(chatId, "Введите название растения:", replyMarkup: new ReplyKeyboardRemove());
        }
        else if (update.Message?.Text?.StartsWith("🗑️ Удалить растение") == true)
        {
            _userStates[chatId] = UserState.WaitingForPlantId;
            await _plantService.MyPlants(chatId);
            await _bot.SendMessage(chatId, "Напишите id растения, которое нужно удалить:", replyMarkup: new ReplyKeyboardRemove());
        }
        else if (_userStates.TryGetValue(chatId, out state))
        {
            if (state == UserState.WaitingForPlantName)
            {
                _userStates[chatId] = UserState.WaitingForWateringFrequency;
                _userPlantData[chatId].PlantName = update.Message.Text;
                await _bot.SendMessage(chatId, "Введите частоту полива:", replyMarkup: Keyboards.Frequency);
            }
            else if (state == UserState.WaitingForWateringFrequency)
            {
                _userStates[chatId] = UserState.WaitingForNotificationHour;
                _userPlantData[chatId].WateringFrequency = update.Message.Text;
                await _bot.SendMessage(chatId, "Введите время, в которое нужно отправлять уведомление(число от 0 до 23):", replyMarkup: new ReplyKeyboardRemove());
            }
            else if (state == UserState.WaitingForNotificationHour)
            {
                if (Byte.TryParse(update.Message.Text, out Byte notificationHour) && notificationHour >= 0 && notificationHour <= 23)
                {
                    _userStates[chatId] = UserState.Idle;
                    _userPlantData[chatId].NotificationHour = notificationHour;
                    UserPlantData data = _userPlantData[chatId];
                    await _plantService.CreatePlant(chatId, data, cancellationToken);
                    _userPlantData.TryRemove(chatId, out _);
                }
                else
                {
                    await _bot.SendMessage(chatId, "Неправильный формат! Напишите время еще раз:",
                    cancellationToken: cancellationToken,
                    replyMarkup: new ReplyKeyboardRemove());
                    return;
                }
            }
            else if (state == UserState.WaitingForPlantId)
            {
                if (int.TryParse(update.Message.Text, out int plantId))
                {
                    await _plantService.DeletePlant(chatId, plantId, cancellationToken);
                    _userStates[chatId] = UserState.Idle;
                }
                else
                {
                    await _bot.SendMessage(chatId, "Неправильный формат! Напишите id растения:",
                    cancellationToken: cancellationToken,
                    replyMarkup: new ReplyKeyboardRemove());
                    return;
                }
            }
            else if (state == UserState.Idle)
                await _bot.SendMessage(chatId, "Используй кнопки меню 👆",
                    cancellationToken: cancellationToken,
                    replyMarkup: Keyboards.Main);
        }
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Произошла ошибка: {exception.Message}");
        return Task.CompletedTask;
    }
}