using Telegram.Bot.Types.ReplyMarkups;
public static class Keyboards
{
    public static readonly ReplyKeyboardMarkup Main = new[]
    {
        new[] {new KeyboardButton ("🌱 Добавить растение")},
        new[] {new KeyboardButton ("📋 Мои растения")},
        new[] {new KeyboardButton ("🗑️ Удалить растение")},
        new[] {new KeyboardButton ("⏰ Настроить время")}
    };

    public static readonly ReplyKeyboardMarkup Frequency = new[]
    {
        new[] {new KeyboardButton ("ежедневно")},
        new[] {new KeyboardButton ("еженедельно")},
        new[] {new KeyboardButton ("ежемесячно")}
    };
}