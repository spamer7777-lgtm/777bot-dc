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
        var mongoUri = Environment.GetEnvironmentVariable("MONGO_URL");
        if (string.IsNullOrEmpty(mongoUri))
            throw new Exception("MONGO_URL is not set in environment variables.");

        var client = new MongoClient(mongoUri);
        var db = client.GetDatabase("777bot"); // Database name
        Users = db.GetCollection<UserData>("users");

        // Create index on Credits for leaderboard
        var indexKeys = Builders<UserData>.IndexKeys.Descending(u => u.Credits);
        Users.Indexes.CreateOne(new CreateIndexModel<UserData>(indexKeys));
    }

    // Get or create user
    public static UserData GetUser(ulong userId)
    {
        var filter = Builders<UserData>.Filter.Eq(u => u.UserId, userId);
        var user = Users.Find(filter).FirstOrDefault();

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

    // Add credits
    public static void AddCredits(ulong userId, int amount)
    {
        var update = Builders<UserData>.Update.Inc(u => u.Credits, amount);
        Users.UpdateOne(u => u.UserId == userId, update, new UpdateOptions { IsUpsert = true });
    }

    // Remove credits
    public static bool RemoveCredits(ulong userId, int amount)
    {
        var user = GetUser(userId);
        if (user.Credits < amount)
            return false;

        var update = Builders<UserData>.Update.Inc(u => u.Credits, -amount);
        Users.UpdateOne(u => u.UserId == userId, update);
        return true;
    }

    // Top users for leaderboard
    public static List<UserData> GetTopUsers(int count)
    {
        return Users.Find(_ => true)
                    .SortByDescending(u => u.Credits)
                    .Limit(count)
                    .ToList();
    }

    // DAILY REWARD HELPERS
    public static bool CanClaimDaily(ulong userId)
    {
        var user = GetUser(userId);
        if (user.LastDailyClaim == null)
            return true;

        return (DateTime.UtcNow - user.LastDailyClaim.Value).TotalHours >= 24;
    }

    public static TimeSpan GetDailyCooldownRemaining(ulong userId)
    {
        var user = GetUser(userId);
        if (user.LastDailyClaim == null)
            return TimeSpan.Zero;

        var nextClaim = user.LastDailyClaim.Value.AddHours(24);
        return nextClaim - DateTime.UtcNow;
    }

    public static void SetDailyClaim(ulong userId)
    {
        var update = Builders<UserData>.Update.Set(u => u.LastDailyClaim, DateTime.UtcNow);
        Users.UpdateOne(u => u.UserId == userId, update, new UpdateOptions { IsUpsert = true });
    }
}

public class UserData
{
    [BsonId]
    [BsonRepresentation(BsonType.Int64)]
    public ulong UserId { get; set; }

    public int Credits { get; set; }

    public DateTime? LastDailyClaim { get; set; }
}
