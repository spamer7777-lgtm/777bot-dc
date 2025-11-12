using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public static class UserDataManager
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "Data", "users.json");
    private static readonly object LockObj = new();
    private static Dictionary<ulong, UserData> Users = new();

    static UserDataManager()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        Load();
    }

    public static void Load()
    {
        if (File.Exists(FilePath))
        {
            var json = File.ReadAllText(FilePath);
            Users = JsonSerializer.Deserialize<Dictionary<ulong, UserData>>(json) ?? new();
        }
    }

    public static void Save()
    {
        lock (LockObj)
        {
            var json = JsonSerializer.Serialize(Users, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
    }

    public static UserData GetUser(ulong userId)
    {
        if (!Users.ContainsKey(userId))
        {
            Users[userId] = new UserData { UserId = userId, Credits = 100 }; // default 100 credits
            Save();
        }
        return Users[userId];
    }

    public static void AddCredits(ulong userId, int amount)
    {
        var user = GetUser(userId);
        user.Credits += amount;
        Save();
    }

    public static bool RemoveCredits(ulong userId, int amount)
    {
        var user = GetUser(userId);
        if (user.Credits < amount)
            return false;

        user.Credits -= amount;
        Save();
        return true;
    }

    // 🏆 Helper: Get top users by credits
    public static List<UserData> GetTopUsers(int count)
    {
        lock (LockObj)
        {
            return Users.Values
                .OrderByDescending(u => u.Credits)
                .Take(count)
                .ToList();
        }
    }
}

public class UserData
{
    public ulong UserId { get; set; }
    public int Credits { get; set; }
}
