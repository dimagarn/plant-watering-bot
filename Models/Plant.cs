using System.Diagnostics.CodeAnalysis;

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

        public Plant() { }
        
        [SetsRequiredMembers]
        public Plant(string name, string wateringFrequency, long chatId, byte notificationHour)
        {
            Name = name;
            WateringFrequency = wateringFrequency;
            ChatId = chatId;
            NotificationHour = notificationHour;
            UpdateNextWateringDate();
        }

        public void UpdateNextWateringDate()
        {
            NextWateringDate = DateTime.UtcNow.AddDays(WateringFrequency switch
            {
                "ежедневно" => 1,
                "еженедельно" => 7,
                "ежемесячно" => 30,
                _ => throw new ArgumentException("Invalid watering frequency")
            });
        }
    }
}