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

        // Railway ustawia PORT w env, lokalnie możesz np. 5000
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
            catch (Exception ex) when (token.IsCancellationRequested)
            {
                Console.WriteLine($"HTTP listener stopped: {ex.Message}");
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
            // Prosty health-check
            if (req.HttpMethod == "GET" && req.Url.AbsolutePath == "/health")
            {
                await WriteJsonAsync(res, new { status = "ok" });
                return;
            }

            // (opcjonalne) zabezpieczenie API key
            var expectedKey = Environment.GetEnvironmentVariable("API_KEY");
            if (!string.IsNullOrEmpty(expectedKey))
            {
                var providedKey = req.Headers["X-Api-Key"];
                if (!string.Equals(providedKey, expectedKey, StringComparison.Ordinal))
                {
                    res.StatusCode = (int)HttpStatusCode.Unauthorized;
                    await WriteJsonAsync(res, new { error = "invalid_api_key" });
                    return;
                }
            }

            if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/check_balance")
            {
                var body = await JsonSerializer.DeserializeAsync<BalanceRequest>(req.InputStream);
                if (body == null)
                {
                    res.StatusCode = (int)HttpStatusCode.BadRequest;
                    await WriteJsonAsync(res, new { error = "invalid_body" });
                    return;
                }

                var user = await UserDataManager.GetUserAsync(body.UserId);
                await WriteJsonAsync(res, new { balance = user.Credits });
                return;
            }

            if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/consume")
            {
                var body = await JsonSerializer.DeserializeAsync<ConsumeRequest>(req.InputStream);
                if (body == null)
                {
                    res.StatusCode = (int)HttpStatusCode.BadRequest;
                    await WriteJsonAsync(res, new { error = "invalid_body" });
                    return;
                }

                bool ok = await UserDataManager.RemoveCreditsAsync(body.UserId, body.Amount);
                await WriteJsonAsync(res, new { success = ok });
                return;
            }

            if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/add_balance")
            {
                var body = await JsonSerializer.DeserializeAsync<ConsumeRequest>(req.InputStream);
                if (body == null)
                {
                    res.StatusCode = (int)HttpStatusCode.BadRequest;
                    await WriteJsonAsync(res, new { error = "invalid_body" });
                    return;
                }

                await UserDataManager.AddCreditsAsync(body.UserId, body.Amount);
                await WriteJsonAsync(res, new { success = true });
                return;
            }

            // Nic nie pasuje
            res.StatusCode = (int)HttpStatusCode.NotFound;
            await WriteJsonAsync(res, new { error = "not_found" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HTTP ERROR] {ex}");
            res.StatusCode = (int)HttpStatusCode.InternalServerError;
            await WriteJsonAsync(res, new { error = "server_error" });
        }
    }

    private static async Task WriteJsonAsync(HttpListenerResponse res, object obj)
    {
        res.ContentType = "application/json; charset=utf-8";
        var json = JsonSerializer.Serialize(obj, JsonOptions);
        var buffer = Encoding.UTF8.GetBytes(json);
        res.ContentLength64 = buffer.Length;
        await res.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        res.Close();
    }

    // DTOs

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
