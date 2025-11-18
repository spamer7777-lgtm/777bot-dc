using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;

public static class HttpApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static async Task StartAsync(CancellationToken token)
    {
        var listener = new HttpListener();

        var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
        var prefix = $"http://0.0.0.0:{port}/";

        listener.Prefixes.Add(prefix);
        listener.Start();

        Console.WriteLine($"✅ HTTP API listening on {prefix}");

        while (!token.IsCancellationRequested)
        {
            HttpListenerContext ctx;

            try { ctx = await listener.GetContextAsync(); }
            catch { break; }

            _ = HandleRequestAsync(ctx);
        }

        listener.Stop();
    }

    private static async Task HandleRequestAsync(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;

        try
        {
            // HEALTH
            if (req.HttpMethod == "GET" && req.Url.AbsolutePath == "/health")
            {
                await WriteJsonAsync(res, new { status = "ok" });
                return;
            }

            // API KEY CHECK (except Activity endpoints)
            var expectedKey = Environment.GetEnvironmentVariable("API_KEY");
            bool isActivityEndpoint =
                req.Url.AbsolutePath.StartsWith("/activity");

            if (!string.IsNullOrEmpty(expectedKey) && !isActivityEndpoint)
            {
                var provided = req.Headers["X-Api-Key"];
                if (provided != expectedKey)
                {
                    res.StatusCode = 401;
                    await WriteJsonAsync(res, new { error = "invalid_api_key" });
                    return;
                }
            }

            //
            // ===========================
            //      ACTIVITY ENDPOINTS
            // ===========================
            //

            // AUTH
            if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/activity/auth")
            {
                var body = await ReadBodyAsync(req);
                var data = JsonSerializer.Deserialize<ActivityAuthRequest>(body, JsonOptions);

                var result = await HandleActivityAuthAsync(data.code);
                await WriteJsonAsync(res, result);
                return;
            }

            // SPIN
            if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/activity/spin")
            {
                var body = await ReadBodyAsync(req);
                var data = JsonSerializer.Deserialize<ActivitySpinRequest>(body, JsonOptions);

                var result = HandleActivitySpin(data.UserId, data.Bet);
                await WriteJsonAsync(res, result);
                return;
            }

            //
            // ===========================
            //     EXISTING ENDPOINTS
            // ===========================
            //

            if (req.HttpMethod == "GET" && req.Url.AbsolutePath == "/balance")
            {
                string userId = req.QueryString["user"];
                int balance = UserDataManager.GetBalance(userId);

                await WriteJsonAsync(res, new { balance });
                return;
            }

            if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/consume")
            {
                var body = await ReadBodyAsync(req);
                var consume = JsonSerializer.Deserialize<ConsumeRequest>(body, JsonOptions);

                UserDataManager.AddBalance(consume.UserId, -consume.Amount);
                await WriteJsonAsync(res, new { ok = true });
                return;
            }

            // NOT FOUND
            res.StatusCode = 404;
            await WriteJsonAsync(res, new { error = "not_found" });
        }
        catch (Exception ex)
        {
            res.StatusCode = 500;
            await WriteJsonAsync(res, new { error = "server_error", detail = ex.Message });
        }
    }

    // =============================
    //             HELPERS
    // =============================

    private static async Task<string> ReadBodyAsync(HttpListenerRequest req)
    {
        using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
        return await reader.ReadToEndAsync();
    }

    private static async Task WriteJsonAsync(HttpListenerResponse res, object obj)
    {
        res.ContentType = "application/json";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(obj, JsonOptions);
        await res.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        res.Close();
    }

    // =============================
    //     DISCORD ACTIVITY AUTH
    // =============================

    private static async Task<object> HandleActivityAuthAsync(string code)
    {
        using var client = new HttpClient();

        var values = new Dictionary<string, string>
        {
            ["client_id"] = Environment.GetEnvironmentVariable("CLIENT_ID"),
            ["client_secret"] = Environment.GetEnvironmentVariable("CLIENT_SECRET"),
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = Environment.GetEnvironmentVariable("ACTIVITY_REDIRECT_URI")
        };

        var tokenResp = await client.PostAsync(
            "https://discord.com/api/oauth2/token",
            new FormUrlEncodedContent(values)
        );

        var tokenJson = await tokenResp.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<OAuthToken>(tokenJson, JsonOptions);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.access_token);

        var userJson = await client.GetStringAsync("https://discord.com/api/users/@me");
        var user = JsonSerializer.Deserialize<DiscordUser>(userJson, JsonOptions);

        int balance = UserDataManager.GetBalance(user.id);

        return new
        {
            userId = user.id,
            balance
        };
    }

    // =============================
    //     SLOT MACHINE LOGIC
    // =============================

    private static object HandleActivitySpin(string userId, int bet)
    {
        int balance = UserDataManager.GetBalance(userId);
        if (balance < bet)
        {
            return new { error = "NOT_ENOUGH_BALANCE" };
        }

        UserDataManager.AddBalance(userId, -bet);

        string[] icons = { "🍒", "🍋", "🍇", "⭐", "💎" };
        var random = new Random();

        string a = icons[random.Next(icons.Length)];
        string b = icons[random.Next(icons.Length)];
        string c = icons[random.Next(icons.Length)];

        int multiplier =
            (a == b && b == c) ? 5 :
            (a == b || b == c || a == c) ? 2 : 0;

        int win = bet * multiplier;

        if (win > 0)
            UserDataManager.AddBalance(userId, win);

        int newBalance = UserDataManager.GetBalance(userId);

        return new
        {
            slots = new[] { a, b, c },
            win,
            newBalance
        };
    }

    // =============================
    //     MODELS
    // =============================

    public class ActivityAuthRequest
    {
        public string code { get; set; }
    }

    public class ActivitySpinRequest
    {
        public string UserId { get; set; }
        public int Bet { get; set; }
    }

    public class OAuthToken
    {
        public string access_token { get; set; }
    }

    public class DiscordUser
    {
        public string id { get; set; }
        public string username { get; set; }
    }

    public class ConsumeRequest
    {
        public string UserId { get; set; }
        public int Amount { get; set; }
    }
}
