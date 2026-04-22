using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

class MessageHandler
{
    private readonly PlantService _plantService;
    private readonly UserService _userService;
    private readonly TelegramBotClient _bot;

    public MessageHandler(AppDbContext dbContext, TelegramBotClient bot)
    {
        _bot = bot;
        _plantService = new PlantService(dbContext, _bot);
        _userService = new UserService(dbContext, _bot);
    }

    public async Task Handle(Update update, CancellationToken cancellationToken, UserStateManager userStateManager)
    {
        long chatId = update.Message.Chat.Id;
        UserState state;
        userStateManager.States.TryAdd(chatId, UserState.Idle);

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
                if (!await _userService.UserExists(chatId))
                {
                    await _bot.SendMessage(chatId, "Для начала введите ваш часовой пояс в формате UtcOffset(Например +3 для Москвы или +5 для Екатеринбурга):", replyMarkup: new ReplyKeyboardRemove());
                    userStateManager.States[chatId] = UserState.WaitingForUtcOffset;
                    return;
                }
                userStateManager.States[chatId] = UserState.WaitingForCreatePlantName;
                userStateManager.PlantData[chatId] = new UserPlantData();
                await _bot.SendMessage(chatId, "Введите название растения:", replyMarkup: new ReplyKeyboardRemove());
                return;
            case "🕰️ Изменить часовой пояс":
                await _bot.SendMessage(chatId, "Введите ваш часовой пояс в формате UtcOffset(Например +3 для Москвы или -6 для Мексики):", replyMarkup: new ReplyKeyboardRemove());
                userStateManager.States[chatId] = UserState.WaitingForUtcOffset;
                return;
        }
        if (userStateManager.States.TryGetValue(chatId, out state))
        {
            switch (state)
            {
                case UserState.WaitingForCreatePlantName:
                    {
                        userStateManager.States[chatId] = UserState.WaitingForCreatePlantWateringFrequency;
                        userStateManager.PlantData[chatId].PlantName = update.Message.Text;
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
                        userStateManager.States[chatId] = UserState.WaitingForCreatePlantNotificationHour;
                        userStateManager.PlantData[chatId].WateringFrequency = update.Message.Text.ToLower();
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
                        userStateManager.States[chatId] = UserState.Idle;
                        userStateManager.PlantData[chatId].NotificationHour = notificationHour;
                        UserPlantData data = userStateManager.PlantData[chatId];
                        await _plantService.CreatePlant(chatId, data, cancellationToken);
                        userStateManager.PlantData.TryRemove(chatId, out _);
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
                        var plantId = userStateManager.PlantData[chatId].PlantId;
                        await _plantService.SetTime(chatId, plantId, notificationHour, cancellationToken);
                        userStateManager.PlantData.TryRemove(chatId, out _);
                        userStateManager.States[chatId] = UserState.Idle;
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
                case UserState.WaitingForUtcOffset:
                    {
                        if (!int.TryParse(update.Message.Text, out int utcOffset) || utcOffset < -12 || utcOffset > 14)
                        {
                            await _bot.SendMessage(chatId, "Неверный формат, попробуй еще раз");
                            return;
                        }
                        if (await _userService.UserExists(chatId))
                        {
                            await _userService.SetUtcOffset(chatId, utcOffset);
                            await _plantService.UpdateAllCronJobs(chatId);
                            userStateManager.States[chatId] = UserState.Idle;
                            return;
                        }
                        await _userService.CreateUser(chatId, utcOffset);
                        userStateManager.States[chatId] = UserState.WaitingForCreatePlantName;
                        userStateManager.PlantData[chatId] = new UserPlantData();
                        await _bot.SendMessage(chatId, "Введите название растения:", replyMarkup: new ReplyKeyboardRemove());
                        break;
                    }
            }
        }
    }
}
