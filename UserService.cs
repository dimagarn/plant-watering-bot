using Telegram.Bot;
using TelegramQuestBot.Models;

public class UserService
{
    private readonly AppDbContext _dbContext;
    private static TelegramBotClient _bot = null!;
    public UserService(AppDbContext dbContext, TelegramBotClient bot)
    {
        _dbContext = dbContext;
        _bot = bot;
    }

    public async Task<bool> UserExists(long chatId)
    {
        var user = await _dbContext.Users.FindAsync(chatId);
        return user is not null;
    }

    public async Task CreateUser(long chatId, int utcOffset)
    {
        if (await UserExists(chatId)) return;
        var user = new User
        {
            ChatId = chatId,
            UtcOffset = utcOffset
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        await _bot.SendMessage(chatId, $"Часовой пояс: {(user.UtcOffset < 0 ? user.UtcOffset : "+" + user.UtcOffset)} успешно установлен!");
    }

    public async Task SetUtcOffset(long chatId, int utcOffset)
    {
        var user = await _dbContext.Users.FindAsync(chatId);
        if (user is null) return;
        user.UtcOffset = utcOffset;
        await _dbContext.SaveChangesAsync();
        await _bot.SendMessage(chatId, $"Часовой пояс: {(user.UtcOffset < 0 ? user.UtcOffset : "+" + user.UtcOffset)} успешно установлен!", replyMarkup: Keyboards.Main);
    }
}