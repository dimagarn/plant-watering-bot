using System.Collections.Concurrent;

public class UserStateManager
{
    public ConcurrentDictionary<long, UserState> States = new();
    public ConcurrentDictionary<long, UserPlantData> PlantData = new();
}