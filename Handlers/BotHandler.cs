using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;


public class BotHandler
{
    private static TelegramBotClient _bot = null!;
    private readonly CallbackHandler _callbackHandler;
    private readonly MessageHandler _messageHandler;
    private readonly UserStateManager userStateManager;

    public BotHandler(AppDbContext dbContext, TelegramBotClient bot)
    {
        _bot = bot;
        _callbackHandler = new CallbackHandler(dbContext, _bot);
        _messageHandler = new MessageHandler(dbContext, _bot);
        userStateManager = new UserStateManager();
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        long chatId;

        if (update.Type == UpdateType.CallbackQuery)
        {
            await _callbackHandler.Handle(update, cancellationToken, userStateManager);
        }

        if (update.Message?.Chat.Id is null || update.Type != UpdateType.Message) return;

        await _messageHandler.Handle(update, cancellationToken, userStateManager);
    }

    public Task HandleError(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Произошла ошибка: {exception.Message}");
        return Task.CompletedTask;
    }
}