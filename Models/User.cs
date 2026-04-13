namespace TelegramQuestBot.Models
{
    public class User
    {
        public long ChatId { get; set; }
        public required int UtcOffset { get; set; }
    }
}