using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

class CallbackHandler
{
    private readonly PlantService _plantService;
    private readonly TelegramBotClient _bot;

    public CallbackHandler(AppDbContext dbContext, TelegramBotClient bot)
    {
        _bot = bot;
        _plantService = new PlantService(dbContext, _bot);
    }

    public async Task Handle(Update update, CancellationToken cancellationToken, UserStateManager userStateManager)
    {
        if (update.Type == UpdateType.CallbackQuery)
        {
            long chatId = update.CallbackQuery.Message.Chat.Id;
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
                    userStateManager.PlantData[chatId] = new UserPlantData { PlantId = plantId };
                    userStateManager.States[chatId] = UserState.WaitingForSetTimeHour;
                    await _bot.SendMessage(chatId, "Введите время, в которое нужно отправлять уведомление(число от 0 до 23):", replyMarkup: new ReplyKeyboardRemove());
                    return;
            }
        }
    }
}