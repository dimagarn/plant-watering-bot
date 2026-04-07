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
        long chatId;
        if (update.Type == UpdateType.CallbackQuery)
        {
            chatId = update.CallbackQuery.Message.Chat.Id;
            string[] plantAction = update.CallbackQuery.Data.Split(":");
            string action = plantAction[0];
            int plantId = int.Parse(plantAction[1]);
            switch (action)
            {
                case "delete":
                    await _plantService.DeletePlant(chatId, plantId, cancellationToken);
                    await _plantService.MyPlants(chatId);
                    return;
                case "settime":
                    _userPlantData[chatId] = new UserPlantData { PlantId = plantId };
                    _userStates[chatId] = UserState.WaitingForSetTimeHour;
                    await _bot.SendMessage(chatId, "Введите время, в которое нужно отправлять уведомление(число от 0 до 23):", replyMarkup: new ReplyKeyboardRemove());
                    return;
            }
        }

        if (update.Message?.Chat.Id is null || update.Type != UpdateType.Message) return;
        chatId = update.Message.Chat.Id;
        UserState state;
        _userStates.TryAdd(chatId, UserState.Idle);

        switch (update.Message?.Text)
        {
            case "/start":
                await _bot.SendMessage(
                    chatId: chatId,
                    text: "🌱 Привет! Давай знакомиться, Я - твой новый универсальный помощник, который будет напоминать тебе как и когда поливать твои растения, чтобы они всегда оставались здоровыми и красивыми!",
                    cancellationToken: cancellationToken,
                    replyMarkup: Keyboards.Main);
                return;
            case "📋 Мои растения": await _plantService.MyPlants(chatId); return;
            case "🌱 Добавить растение":
                _userStates[chatId] = UserState.WaitingForCreatePlantName;
                _userPlantData[chatId] = new UserPlantData();
                await _bot.SendMessage(chatId, "Введите название растения:", replyMarkup: new ReplyKeyboardRemove());
                return;
        }
        if (_userStates.TryGetValue(chatId, out state))
            switch (state)
            {
                case UserState.WaitingForCreatePlantName:
                    {
                        _userStates[chatId] = UserState.WaitingForCreatePlantWateringFrequency;
                        _userPlantData[chatId].PlantName = update.Message.Text;
                        await _bot.SendMessage(chatId, "Введите частоту полива:", replyMarkup: Keyboards.Frequency);
                        break;
                    }
                case UserState.WaitingForCreatePlantWateringFrequency:
                    {
                        string[] validFrequencies = { "ежедневно", "еженедельно", "ежемесячно" };
                        if (!validFrequencies.Contains(update.Message.Text?.ToLower()))
                        {
                            await _bot.SendMessage(chatId, "❌ Выберите значение из предложенных кнопок!",
                                replyMarkup: Keyboards.Frequency);
                            return;
                        }
                        _userStates[chatId] = UserState.WaitingForCreatePlantNotificationHour;
                        _userPlantData[chatId].WateringFrequency = update.Message.Text.ToLower();
                        await _bot.SendMessage(chatId, "Введите время, в которое нужно отправлять уведомление(число от 0 до 23):", replyMarkup: new ReplyKeyboardRemove());
                        break;
                    }
                case UserState.WaitingForCreatePlantNotificationHour:
                    {
                        if (!Byte.TryParse(update.Message.Text, out Byte notificationHour) || notificationHour > 23)
                        {
                            await _bot.SendMessage(chatId, "Неправильный формат! Напишите время еще раз:",
                            cancellationToken: cancellationToken,
                            replyMarkup: new ReplyKeyboardRemove());
                            return;
                        }
                        _userStates[chatId] = UserState.Idle;
                        _userPlantData[chatId].NotificationHour = notificationHour;
                        UserPlantData data = _userPlantData[chatId];
                        await _plantService.CreatePlant(chatId, data, cancellationToken);
                        _userPlantData.TryRemove(chatId, out _);
                        break;
                    }
                case UserState.WaitingForSetTimeHour:
                    {
                        if (!Byte.TryParse(update.Message.Text, out Byte notificationHour) || notificationHour > 23)
                        {
                            await _bot.SendMessage(chatId, "Неправильный формат! Напишите время еще раз:",
                            cancellationToken: cancellationToken,
                            replyMarkup: new ReplyKeyboardRemove());
                            return;
                        }
                        var plantId = _userPlantData[chatId].PlantId;
                        await _plantService.SetTime(chatId, plantId, notificationHour, cancellationToken);
                        _userPlantData.TryRemove(chatId, out _);
                        _userStates[chatId] = UserState.Idle;
                        await _plantService.MyPlants(chatId);
                        break;
                    }
                case UserState.Idle:
                    {
                        await _bot.SendMessage(chatId, "Используй кнопки меню 👆",
                            cancellationToken: cancellationToken,
                            replyMarkup: Keyboards.Main);
                        break;
                    }
            }
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Произошла ошибка: {exception.Message}");
        return Task.CompletedTask;
    }
}