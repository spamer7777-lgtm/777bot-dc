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
            if (req.HttpMethod == "GET" && req.Url.AbsolutePath == "/health")
            {
                await WriteJsonAsync(res, new { status = "ok" });
                return;
            }

            var expectedKey = Environment.GetEnvironmentVariable("API_KEY");
            bool isActivityEndpoint = req.Url.AbsolutePath.StartsWith("/activity/");

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

            // ===============================
            // DISCORD ACTIVITY ENDPOINTS
            // ===============================

            if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/activity/auth")
            {
                var body = await ReadBodyAsync(req);
                var data = JsonSerializer.Deserialize<ActivityAuthRequest>(body, JsonOptions);

                var authResult = await HandleActivityAuthAsync(data.code);
                await WriteJsonAsync(res, authResult);
                return;
            }

            if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/activity/spin")
            {
                var body = await ReadBodyAsync(req);
                var data = JsonSerializer.Deserialize<ActivitySpinRequest>(body, JsonOptions);

                var spinResult = await HandleActivitySpinAsync(data.userId, data.bet);
                await WriteJsonAsync(res, spinResult);
                return;
            }

            // ===============================
            // EXISTING BOT API
            // ===============================

            if (req.HttpMethod == "GET" && req.Url.AbsolutePath == "/balance")
            {
                if (!ulong.TryParse(req.QueryString["user"], out ulong uid))
                {
                    res.StatusCode = 400;
                    await WriteJsonAsync(res, new { error = "invalid_user_id" });
                    return;
                }

                var user = await UserDataManager.GetUserAsync(uid);
                await WriteJsonAsync(res, new { balance = user.Credits });
                return;
            }

            if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/consume")
            {
                var body = await ReadBodyAsync(req);
                var consume = JsonSerializer.Deserialize<ConsumeRequest>(body, JsonOptions);

                ulong uid = ulong.Parse(consume.UserId);
                await UserDataManager.RemoveCreditsAsync(uid, consume.Amount);

                await WriteJsonAsync(res, new { ok = true });
                return;
            }

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
    // HELPERS
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
    // DISCORD ACTIVITY AUTH
    // =============================

    private static async Task<object> HandleActivityAuthAsync(string code)
    {
        Console.WriteLine("====== ACTIVITY AUTH ======");

        using var client = new HttpClient();

        var form = new Dictionary<string, string>
        {
            ["client_id"] = Environment.GetEnvironmentVariable("CLIENT_ID"),
            ["client_secret"] = Environment.GetEnvironmentVariable("CLIENT_SECRET"),
            ["grant_type"] = "authorization_code",
            ["code"] = code
            // IMPORTANT: NO redirect_uri FOR ACTIVITIES
        };

        var tokenResp = await client.PostAsync(
            "https://discord.com/api/oauth2/token",
            new FormUrlEncodedContent(form)
        );

        var tokenJson = await tokenResp.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<OAuthToken>(tokenJson, JsonOptions);

        if (token == null || string.IsNullOrEmpty(token.access_token))
        {
            Console.WriteLine("❌ OAuth token failed");
            return new { error = "invalid_oauth" };
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.access_token);

        var userJson = await client.GetStringAsync("https://discord.com/api/users/@me");
        var user = JsonSerializer.Deserialize<DiscordUser>(userJson, JsonOptions);

        Console.WriteLine($"[AUTH] Discord UserID returned = {user.id}");

        ulong uid = ulong.Parse(user.id);

        var dbUser = await UserDataManager.GetUserAsync(uid);

        Console.WriteLine($"[AUTH] MongoDB credits = {dbUser.Credits}");

        return new
        {
            userId = user.id,
            balance = dbUser.Credits
        };
    }

    // =============================
    // ACTIVITY SPIN LOGIC
    // =============================

    private static async Task<object> HandleActivitySpinAsync(string userId, int bet)
    {
        Console.WriteLine("====== ACTIVITY SPIN ======");
        Console.WriteLine($"[SPIN] Incoming spin request: userId={userId}, bet={bet}");

        ulong uid = ulong.Parse(userId);
        var user = await UserDataManager.GetUserAsync(uid);

        Console.WriteLine($"[SPIN] DB credits before spin = {user.Credits}");

        if (user.Credits < bet)
        {
            Console.WriteLine("[SPIN] ❌ Not enough credits!");
            return new { error = "NOT_ENOUGH_BALANCE" };
        }

        await UserDataManager.RemoveCreditsAsync(uid, bet);

        string[] icons = { "🍒", "🍋", "🍇", "⭐", "💎" };
        var rng = new Random();

        string a = icons[rng.Next(icons.Length)];
        string b = icons[rng.Next(icons.Length)];
        string c = icons[rng.Next(icons.Length)];

        int multiplier =
            (a == b && b == c) ? 5 :
            (a == b || b == c || a == c) ? 2 : 0;

        int win = bet * multiplier;

        if (win > 0)
            await UserDataManager.AddCreditsAsync(uid, win);

        var updated = await UserDataManager.GetUserAsync(uid);

        Console.WriteLine($"[SPIN] DB credits AFTER spin = {updated.Credits}");

        return new
        {
            slots = new[] { a, b, c },
            win,
            newBalance = updated.Credits
        };
    }

    // =============================
    // MODELS
    // =============================

    public class ActivityAuthRequest
    {
        public string code { get; set; }
    }

    public class ActivitySpinRequest
    {
        public string userId { get; set; }  // camelCase for JSON!!!
        public int bet { get; set; }        // camelCase for JSON!!!
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
