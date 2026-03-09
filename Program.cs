using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

class Program
{
    static async Task Main()
    {
        var token = "7027999847:AAGvr5o-FUpIh5awURFezZyrVx50uhTSdBA";
        using var cts = new CancellationTokenSource();
        var bot = new TelegramBotClient(token, cancellationToken: cts.Token);

        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message && update.Message?.Text == "/start")
            {
                await bot.SendMessage(
                    chatId: update.Message.Chat.Id,
                    text: "🌱 Привет! Давай знакомиться, Я - твой новый универсальный помощник, который будет напоминать тебе как и когда поливать твои растения, чтобы они всегда оставались здоровыми и красивыми!",
                    cancellationToken: cancellationToken);
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