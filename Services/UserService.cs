using Telegram.Bot;
using TelegramQuestBot.Models;
using Microsoft.EntityFrameworkCore;

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
        return await _dbContext.Users.AnyAsync(u => u.ChatId == chatId);
    }

    public async Task CreateUser(long chatId, int utcOffset)
    {
        if (await UserExists(chatId)) return;
        var user = new User
        {
            ChatId = chatId,
            UtcOffset = utcOffset
        };
        await _dbContext.Users.AddAsync(user);
        if (await _dbContext.SaveChangesAsync() > 0)
        {
            await _bot.SendMessage(chatId, $"Часовой пояс: {(user.UtcOffset < 0 ? user.UtcOffset : "+" + user.UtcOffset)} успешно установлен!");
        }
        else
        {
            await _bot.SendMessage(chatId, "Произошла ошибка, попробуйте еще раз");
        }
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