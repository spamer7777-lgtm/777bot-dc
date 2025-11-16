using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        if (!mongoUrl.Contains("authSource"))
            mongoUrl += "?authSource=admin";

        var client = new MongoClient(mongoUrl);
        var database = client.GetDatabase(mongoDb);
        Users = database.GetCollection<UserData>("users");

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
                LastDailyClaim = null,
                LastMessageReward = null,
                Exp = 0,
                Level = 1,
                Streak = 0,
                Bio = "Brak opisu.",
                ProfileBackgroundUrl = null
            };
            await Users.InsertOneAsync(user);
        }
        return user;
    }

    // ------------------ CREDITS ------------------
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

    // ------------------ EXP / LEVEL ------------------
    public static async Task AddExpAsync(ulong userId, int amount)
    {
        var user = await GetUserAsync(userId);

        int newExp = user.Exp + amount;
        int levelReq = user.Level * 100; // prosty wzór: 100 * level

        if (newExp >= levelReq)
        {
            newExp -= levelReq;

            var update = Builders<UserData>.Update
                .Set(u => u.Exp, newExp)
                .Inc(u => u.Level, 1);

            await Users.UpdateOneAsync(u => u.UserId == userId, update);

            Console.WriteLine($"⭐ {userId} awansował na poziom {user.Level + 1}");
        }
        else
        {
            var update = Builders<UserData>.Update.Set(u => u.Exp, newExp);
            await Users.UpdateOneAsync(u => u.UserId == userId, update);
        }
    }

    // ------------------ DAILY REWARD ------------------
    public static async Task<bool> CanClaimDailyAsync(ulong userId)
    {
        var user = await GetUserAsync(userId);
        return user.LastDailyClaim == null || (DateTime.UtcNow - user.LastDailyClaim.Value).TotalHours >= 24;
    }

    public static async Task<TimeSpan> GetDailyCooldownRemainingAsync(ulong userId)
    {
        var user = await GetUserAsync(userId);
        if (user.LastDailyClaim == null) return TimeSpan.Zero;
        return user.LastDailyClaim.Value.AddHours(24) - DateTime.UtcNow;
    }

    public static async Task SetDailyClaimAsync(ulong userId)
    {
        var user = await GetUserAsync(userId);

        bool keepStreak = false;
        if (user.LastDailyClaim != null)
        {
            var diff = DateTime.UtcNow - user.LastDailyClaim.Value;
            if (diff.TotalHours < 48) // odebrane w ciągu 2 dni -> streak +
                keepStreak = true;
        }

        var update = Builders<UserData>.Update
            .Set(u => u.LastDailyClaim, DateTime.UtcNow)
            .Set(u => u.Streak, keepStreak ? user.Streak + 1 : 1);

        await Users.UpdateOneAsync(u => u.UserId == userId, update);
    }

    // ------------------ MESSAGE REWARD ------------------
    public static async Task<bool> CanEarnMessageRewardAsync(ulong userId, TimeSpan cooldown)
    {
        var user = await GetUserAsync(userId);
        if (user.LastMessageReward == null) return true;
        return (DateTime.UtcNow - user.LastMessageReward.Value) >= cooldown;
    }

    public static async Task SetMessageRewardAsync(ulong userId)
    {
        var update = Builders<UserData>.Update.Set(u => u.LastMessageReward, DateTime.UtcNow);
        await Users.UpdateOneAsync(u => u.UserId == userId, update);
    }

    // ------------------ PROFILE FIELDS (BIO / TŁO) ------------------
    public static async Task SetBioAsync(ulong userId, string bio)
    {
        var update = Builders<UserData>.Update.Set(u => u.Bio, bio);
        await Users.UpdateOneAsync(u => u.UserId == userId, update);
    }

    public static async Task SetProfileBackgroundAsync(ulong userId, string url)
    {
        var update = Builders<UserData>.Update.Set(u => u.ProfileBackgroundUrl, url);
        await Users.UpdateOneAsync(u => u.UserId == userId, update);
    }

// ------------------ LEADERBOARD ------------------
public static async Task<List<(ulong UserId, int Credits)>> GetTopUsersLeaderboardAsync(int count)
{
    var topList = new List<(ulong, int)>();

    var projection = Builders<UserData>.Projection
        .Include("userId")
        .Include("credits");

    var cursor = await Users.Find(FilterDefinition<UserData>.Empty)
                            .Project(projection)
                            .Sort(Builders<UserData>.Sort.Descending("credits"))
                            .Limit(count)
                            .ToListAsync();

    foreach (var doc in cursor)
    {
        if (doc.Contains("userId") && doc["userId"].IsInt64)
        {
            ulong uid = (ulong)doc["userId"].AsInt64;
            int credits = doc.Contains("credits") ? doc["credits"].AsInt32 : 0;
            topList.Add((uid, credits));
        }
    }

    return topList;
    }
}    
// ------------------ USER DATA ------------------
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

    [BsonElement("lastMessageReward")]
    public DateTime? LastMessageReward { get; set; } // for message reward cooldown

    [BsonElement("exp")]
    public int Exp { get; set; }

    [BsonElement("level")]
    public int Level { get; set; } = 1;

    [BsonElement("streak")]
    public int Streak { get; set; } = 0;

    [BsonElement("bio")]
    public string Bio { get; set; } = "Brak opisu.";

    [BsonElement("profileBackgroundUrl")]
    public string ProfileBackgroundUrl { get; set; }
}
