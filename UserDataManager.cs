using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

public static class UserDataManager
{
    private static readonly IMongoCollection<UserData> Users;

    static UserDataManager()
    {
        var mongoUrl = Environment.GetEnvironmentVariable("MONGO_URL");
        var mongoDb = Environment.GetEnvironmentVariable("MONGO_DB") ?? "777bot";

        if (string.IsNullOrEmpty(mongoUrl))
            throw new Exception("MONGO_URL is not set in environment variables.");

        var client = new MongoClient(mongoUrl);
        var database = client.GetDatabase(mongoDb);

        Users = database.GetCollection<UserData>("users");
    }

    public static UserData GetUser(ulong userId)
    {
        var user = Users.Find(u => u.UserId == userId).FirstOrDefault();
        if (user == null)
        {
            user = new UserData
            {
                UserId = userId,
                Credits = 100,
                LastDailyClaim = null
            };
            Users.InsertOne(user);
        }
        return user;
    }

    public static void AddCredits(ulong userId, int amount)
    {
        var user = GetUser(userId);
        user.Credits += amount;
        Users.ReplaceOne(u => u.UserId == userId, user);
    }

    public static bool RemoveCredits(ulong userId, int amount)
    {
        var user = GetUser(userId);
        if (user.Credits < amount) return false;

        user.Credits -= amount;
        Users.ReplaceOne(u => u.UserId == userId, user);
        return true;
    }

    public static List<UserData> GetTopUsers(int count)
    {
        return Users.Find(FilterDefinition<UserData>.Empty)
                    .SortByDescending(u => u.Credits)
                    .Limit(count)
                    .ToList();
    }

    // Daily helpers
    public static bool CanClaimDaily(ulong userId)
    {
        var user = GetUser(userId);
        if (user.LastDailyClaim == null) return true;
        return (DateTime.UtcNow - user.LastDailyClaim.Value).TotalHours >= 24;
    }

    public static TimeSpan GetDailyCooldownRemaining(ulong userId)
    {
        var user = GetUser(userId);
        if (user.LastDailyClaim == null) return TimeSpan.Zero;
        var nextClaim = user.LastDailyClaim.Value.AddHours(24);
        return nextClaim - DateTime.UtcNow;
    }

    public static void SetDailyClaim(ulong userId)
    {
        var user = GetUser(userId);
        user.LastDailyClaim = DateTime.UtcNow;
        Users.ReplaceOne(u => u.UserId == userId, user);
    }
}

public class UserData
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userId")]
    public ulong UserId { get; set; }

    [BsonElement("credits")]
    public int Credits { get; set; }

    [BsonElement("lastDailyClaim")]
    public DateTime? LastDailyClaim { get; set; }
}
