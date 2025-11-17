using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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

            try
            {
                ctx = await listener.GetContextAsync();
            }
            catch
            {
                break;
            }

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
            if (!string.IsNullOrEmpty(expectedKey))
            {
                var provided = req.Headers["X-Api-Key"];
                if (provided != expectedKey)
                {
                    res.StatusCode = 401;
                    await WriteJsonAsync(res, new { error = "invalid_api_key" });
                    return;
                }
            }

            if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/check_balance")
            {
                var body = await JsonSerializer.DeserializeAsync<BalanceRequest>(req.InputStream);
                var user = await UserDataManager.GetUserAsync(body.UserId);

                await WriteJsonAsync(res, new { balance = user.Credits });
                return;
            }

            if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/consume")
            {
                var body = await JsonSerializer.DeserializeAsync<ConsumeRequest>(req.InputStream);
                bool ok = await UserDataManager.RemoveCreditsAsync(body.UserId, body.Amount);

                await WriteJsonAsync(res, new { success = ok });
                return;
            }

            if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/add_balance")
            {
                var body = await JsonSerializer.DeserializeAsync<ConsumeRequest>(req.InputStream);
                await UserDataManager.AddCreditsAsync(body.UserId, body.Amount);

                await WriteJsonAsync(res, new { success = true });
                return;
            }

            res.StatusCode = 404;
            await WriteJsonAsync(res, new { error = "not_found" });
        }
        catch (Exception ex)
        {
            Console.WriteLine("[HTTP ERROR] " + ex.Message);
            res.StatusCode = 500;
            await WriteJsonAsync(res, new { error = "server_error" });
        }
    }

    private static async Task WriteJsonAsync(HttpListenerResponse res, object obj)
    {
        res.ContentType = "application/json";
        var json = JsonSerializer.Serialize(obj, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        res.Close();
    }

    private class BalanceRequest
    {
        public ulong UserId { get; set; }
    }

    private class ConsumeRequest
    {
        public ulong UserId { get; set; }
        public int Amount { get; set; }
    }
}
