using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Bson;

public static class UserDataManager
{
    private static readonly IMongoCollection<UserData> Users;

    static UserDataManager()
    {
        var mongoUrl = Environment.GetEnvironmentVariable("MONGO_URL");
        var mongoDb = Environment.GetEnvironmentVariable("MONGO_DB") ?? "777bot";

        if (string.IsNullOrEmpty(mongoUrl))
            throw new Exception("MONGO_URL is not set in environment variables.");

        // Ensure authentication source is set
        if (!mongoUrl.Contains("authSource"))
            mongoUrl += "?authSource=admin";

        var client = new MongoClient(mongoUrl);
        var database = client.GetDatabase(mongoDb);
        Users = database.GetCollection<UserData>("users");

        // Test connection
        try
        {
            var count = Users.CountDocuments(FilterDefinition<UserData>.Empty);
            Console.WriteLine($"✅ MongoDB connected! Users count: {count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ MongoDB connection failed: {ex.Message}");
        }
    }

    // ------------------ USER OPERATIONS ------------------

    public static async Task<UserData> GetUserAsync(ulong userId)
    {
        var user = await Users.Find(u => u.UserId == userId).FirstOrDefaultAsync();
        if (user == null)
        {
            user = new UserData
            {
                UserId = userId,
                Credits = 100,
                LastDailyClaim = null
            };
            await Users.InsertOneAsync(user);
        }
        return user;
    }

    public static async Task AddCreditsAsync(ulong userId, int amount)
    {
        var update = Builders<UserData>.Update.Inc(u => u.Credits, amount);
        await Users.UpdateOneAsync(u => u.UserId == userId, update);
    }

    public static async Task<bool> RemoveCreditsAsync(ulong userId, int amount)
    {
        var user = await GetUserAsync(userId);
        if (user.Credits < amount) return false;

        var update = Builders<UserData>.Update.Inc(u => u.Credits, -amount);
        await Users.UpdateOneAsync(u => u.UserId == userId, update);
        return true;
    }

    // ------------------ DAILY REWARD ------------------

    public static async Task<bool> CanClaimDailyAsync(ulong userId)
    {
        var user = await GetUserAsync(userId);
        if (user.LastDailyClaim == null) return true;
        return (DateTime.UtcNow - user.LastDailyClaim.Value).TotalHours >= 24;
    }

    public static async Task<TimeSpan> GetDailyCooldownRemainingAsync(ulong userId)
    {
        var user = await GetUserAsync(userId);
        if (user.LastDailyClaim == null) return TimeSpan.Zero;
        var nextClaim = user.LastDailyClaim.Value.AddHours(24);
        return nextClaim - DateTime.UtcNow;
    }

    public static async Task SetDailyClaimAsync(ulong userId)
    {
        var update = Builders<UserData>.Update.Set(u => u.LastDailyClaim, DateTime.UtcNow);
        await Users.UpdateOneAsync(u => u.UserId == userId, update);
    }

    // ------------------ LEADERBOARD ------------------

    public static async Task<List<UserData>> GetTopUsersAsync(int count)
    {
        return await Users.Find(FilterDefinition<UserData>.Empty)
                          .SortByDescending(u => u.Credits)
                          .Limit(count)
                          .ToListAsync();
    }
}

public class UserData
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString(); // <-- CHANGE HERE

    [BsonElement("userId")]
    public ulong UserId { get; set; }

    [BsonElement("credits")]
    public int Credits { get; set; }

    [BsonElement("lastDailyClaim")]
    public DateTime? LastDailyClaim { get; set; }
}
