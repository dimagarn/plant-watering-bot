using Microsoft.EntityFrameworkCore;

namespace TelegramQuestBot.Models
{
    public class Plant
    {
        public int Id { get; set; }
        public long ChatId { get; set; }
        public string Name { get; set; }
        public string WateringFrequency { get; set; }
        public DateTime NextWateringDate { get; set; }
    }
}