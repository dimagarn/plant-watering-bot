using Telegram.Bot.Types.ReplyMarkups;
public static class Keyboards
{
    public static readonly ReplyKeyboardMarkup Main = new[]
    {
        new[] {new KeyboardButton ("🌱 Добавить растение")},
        new[] {new KeyboardButton ("📋 Мои растения")}
    };

    public static readonly ReplyKeyboardMarkup Frequency = new[]
    {
        new[] {new KeyboardButton ("ежедневно")},
        new[] {new KeyboardButton ("еженедельно")},
        new[] {new KeyboardButton ("ежемесячно")}
    };

    public static InlineKeyboardMarkup PlantActions(int plantId)
    {
        return new[]
        {
            new[]
            {
                new InlineKeyboardButton { Text = "🗑️ Удалить", CallbackData = $"delete:{plantId}" },
                new InlineKeyboardButton { Text = "⏰ Настроить время", CallbackData = $"settime:{plantId}" }
            }
        };
    }
}