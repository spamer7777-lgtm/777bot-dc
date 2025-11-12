using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System.Threading.Tasks;

public static class UserDataManager
{
    private static readonly IMongoCollection<UserData> UsersCollection;

    static UserDataManager()
    {
        // Load MongoDB connection info from environment variables
        var mongoUser = Environment.GetEnvironmentVariable("MONGOUSER");
        var mongoPassword = Environment.GetEnvironmentVariable("MONGOPASSWORD");
        var mongoHost = Environment.GetEnvironmentVariable("MONGOHOST");
        var mongoPort = Environment.GetEnvironmentVariable("MONGOPORT") ?? "27017";

        var connectionString = $"mongodb://{mongoUser}:{mongoPassword}@{mongoHost}:{mongoPort}";
        var client = new MongoClient(connectionString);

        // Choose a database and collection
        var database = client.GetDatabase("777bot"); // Database name
        UsersCollection = database.GetCollection<UserData>("users"); // Collection name
    }

    // Get user, create default if not exists
    public static UserData GetUser(ulong userId)
    {
        var user = UsersCollection.Find(u => u.UserId == userId).FirstOrDefault();
        if (user == null)
        {
            user = new UserData
            {
                UserId = userId,
                Credits = 100,
                LastDailyClaim = null
            };
            UsersCollection.InsertOne(user);
        }
        return user;
    }

    // Add credits to user
    public static void AddCredits(ulong userId, int amount)
    {
        var update = Builders<UserData>.Update.Inc(u => u.Credits, amount);
        UsersCollection.UpdateOne(u => u.UserId == userId, update);
    }

    // Remove credits from user, returns false if not enough
    public static bool RemoveCredits(ulong userId, int amount)
    {
        var user = GetUser(userId);
        if (user.Credits < amount) return false;

        var update = Builders<UserData>.Update.Inc(u => u.Credits, -amount);
        UsersCollection.UpdateOne(u => u.UserId == userId, update);
        return true;
    }

    // Leaderboard: top users by credits
    public static List<UserData> GetTopUsers(int count)
    {
        return UsersCollection.Find(_ => true)
                              .SortByDescending(u => u.Credits)
                              .Limit(count)
                              .ToList();
    }

    // DAILY REWARD HELPERS
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
        var update = Builders<UserData>.Update.Set(u => u.LastDailyClaim, DateTime.UtcNow);
        UsersCollection.UpdateOne(u => u.UserId == userId, update);
    }
}

public class UserData
{
    [BsonId]
    [BsonRepresentation(BsonType.Int64)]
    public ulong UserId { get; set; }

    [BsonElement("credits")]
    public int Credits { get; set; }

    [BsonElement("lastDailyClaim")]
    public DateTime? LastDailyClaim { get; set; }
}
