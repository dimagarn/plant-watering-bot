using Microsoft.EntityFrameworkCore;

namespace TelegramQuestBot.Models
{
    public class Plant
    {
        public int Id { get; set; }
        public long ChatId { get; set; }
        public required string Name { get; set; }
        public required string WateringFrequency { get; set; }
        public DateTime NextWateringDate { get; set; }
        public byte NotificationHour { get; set; } = 9;
    }
}