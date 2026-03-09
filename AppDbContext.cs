using Microsoft.EntityFrameworkCore;
using TelegramQuestBot.Models;

public class AppDbContext : DbContext
{
    public DbSet<Plant> Plants { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql("Host=localhost;Database=plantsdb;Username=dimagarn;Password=telegramquestbot");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Plant>().HasKey(p => p.Id);
    }

}