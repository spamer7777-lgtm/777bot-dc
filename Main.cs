using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;

public static class Bot
{
    private const ulong TubasStickerId = 1435403416733225174;
    private const ulong RozkminkaStickerId = 1435646701137428511;

    public static readonly DiscordSocketClient Client = new(new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
    });

    private static InteractionService Service;
    private static Timer timer;

    // Global HttpClient
    public static readonly HttpClient Http = new HttpClient(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        UseProxy = true
    })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    static Bot()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 DiscordBot/1.0");
        Http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // Message credit cooldowns
    private static readonly Dictionary<ulong, DateTime> messageCreditCooldowns = new();
    private static readonly int creditCooldownSeconds = 60;
    private static readonly int creditAmountMin = 1;
    private static readonly int creditAmountMax = 5;

    public static async Task Main()
    {
        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        if (string.IsNullOrEmpty(token))
            throw new ArgumentException("DISCORD_TOKEN is not set!");

        Client.Ready += Ready;
        Client.Log += Log;
        Client.MessageReceived += MessageReceivedHandler;

        // Safe button wrapper
        Client.ButtonExecuted += HandleButtonSafeWrapper;

        await Client.LoginAsync(TokenType.Bot, token);
        await Client.StartAsync();

        Console.WriteLine("✅ Bot + HTTP API running.");

        await Task.Delay(-1);
    }

    // ====================================================
    // BUTTON WRAPPER — safe execution
    // ====================================================
    private static async Task HandleButtonSafeWrapper(SocketMessageComponent component)
    {
        try
        {
            await Commands.NoGroup.HandleRouletteButtonsStatic(component);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BUTTON ERROR] {ex}");

            try
            {
                if (!component.HasResponded)
                {
                    await component.RespondAsync(
                        $"❌ Błąd:\n```\n{ex.Message}\n```",
                        ephemeral: true
                    );
                }
            }
            catch { }
        }
    }

    // ====================================================
    // MESSAGE CREDITS
    // ====================================================
    private static async Task MessageReceivedHandler(SocketMessage message)
    {
        if (message.Author.Id == Client.CurrentUser.Id) return;
        if (message.Author is not SocketGuildUser user) return;
        if (message.Attachments.Any()) return;

        string contentLower = message.Content.ToLowerInvariant();
        string[] words = contentLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        bool containsXddd = words.Contains("xddddd");
        bool containsTubas = words.Contains("tubas");
        bool containsRozkminka = words.Contains("co?");

        int triggerCount = new[] { containsXddd, containsTubas, containsRozkminka }.Count(b => b);

        if (triggerCount == 0)
            await HandleMessageCredits(user);

        if (triggerCount != 1) return;

        if (containsXddd)
            await HandleXdddDetection(message, user);
        else if (containsTubas)
            await HandleTubasDetection(message, user);
        else if (containsRozkminka)
            await HandleRozkminkaDetection(message, user);
    }

    private static async Task HandleMessageCredits(SocketGuildUser user)
    {
        if (messageCreditCooldowns.TryGetValue(user.Id, out DateTime lastClaim))
        {
            if ((DateTime.UtcNow - lastClaim).TotalSeconds < creditCooldownSeconds)
                return;
        }

        var rand = new Random();
        int reward = rand.Next(creditAmountMin, creditAmountMax + 1);

        await UserDataManager.AddCreditsAsync(user.Id, reward);
        messageCreditCooldowns[user.Id] = DateTime.UtcNow;

        Console.WriteLine($"[CREDIT DROP] +{reward} → {user.Username}");
    }

    // ====================================================
    // REACTION TRIGGERS
    // ====================================================

    private static async Task HandleXdddDetection(SocketMessage message, SocketGuildUser user)
    {
        Console.WriteLine($"[XDDD] {user.Username}");

        var kekwEmoji = Client.Guilds.SelectMany(g => g.Emotes)
            .FirstOrDefault(e => e.Name.Equals("kekw", StringComparison.OrdinalIgnoreCase));

        if (kekwEmoji != null)
            await message.Channel.SendMessageAsync(kekwEmoji.ToString());
    }

    private static async Task HandleTubasDetection(SocketMessage message, SocketGuildUser user)
    {
        Console.WriteLine($"[TUBAS] {user.Username}");
        if (message.Channel is SocketTextChannel chan)
        {
            var sticker = chan.Guild.Stickers.FirstOrDefault(s => s.Id == TubasStickerId);
            if (sticker != null)
                await chan.SendMessageAsync(stickers: new[] { sticker });
        }
    }

    private static async Task HandleRozkminkaDetection(SocketMessage message, SocketGuildUser user)
    {
        Console.WriteLine($"[ROZKMINKA] {user.Username}");
        if (message.Channel is SocketTextChannel chan)
        {
            var sticker = chan.Guild.Stickers.FirstOrDefault(s => s.Id == RozkminkaStickerId);
            if (sticker != null)
                await chan.SendMessageAsync(stickers: new[] { sticker });
        }
    }

    // ====================================================
    // READY — FIXED WITH SAFE GLOBAL COMMAND SYNC
    // ====================================================
    private static async Task Ready()
    {
        try
        {
            Service = new InteractionService(Client, new InteractionServiceConfig
            {
                ThrowOnError = true,
                UseCompiledLambda = true
            });

            await Service.AddModulesAsync(Assembly.GetEntryAssembly(), null);

            // DO NOT USE BULK OVERWRITE
            await RegisterGlobalCommandsSafe();

            Client.InteractionCreated += InteractionCreated;
            Service.SlashCommandExecuted += SlashCommandResulted;

            Console.WriteLine($"✅ Ready! Loaded {Service.Modules.Count} slash command modules.");

            await Client.SetGameAsync("777 Slots");

            string[] statuses = { "No Siemano!", "Ale kto pytał?", "Ale sigiemki tutaj" };
            int idx = 0;
            bool flip = false;

            timer = new Timer(async _ =>
            {
                if (flip)
                    await Client.SetGameAsync("777 Slots");
                else
                    await Client.SetCustomStatusAsync(statuses[idx]);

                flip = !flip;
                idx = (idx + 1) % statuses.Length;

            }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[READY ERROR] {ex}");
        }
    }

    // ====================================================
    // SAFE GLOBAL COMMAND REGISTRATION (NO BULK OVERWRITE)
    // ====================================================
    private static async Task RegisterGlobalCommandsSafe()
    {
        try
        {
            var appInfo = await Client.GetApplicationInfoAsync();

            Console.WriteLine("🌍 Syncing global commands (safe mode, no bulk overwrite)…");

            var modules = Service.Modules.SelectMany(m => m.SlashCommands);
            var built = new List<ApplicationCommandProperties>();

            foreach (var cmd in modules)
            {
                built.Add(cmd.BuildCommand(global: true));
            }

            var existing = await Client.Rest.GetGlobalApplicationCommands(appInfo.Id);
            var existingDict = existing.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var newCmd in built)
            {
                var name = newCmd.Name;

                if (existingDict.TryGetValue(name, out var existingCmd))
                {
                    await Client.Rest.EditGlobalCommand(appInfo.Id, existingCmd.Id, newCmd);
                    Console.WriteLine($"🔄 Updated global command /{name}");
                }
                else
                {
                    await Client.Rest.CreateGlobalCommand(appInfo.Id, newCmd);
                    Console.WriteLine($"➕ Created global command /{name}");
                }
            }

            Console.WriteLine("✔ Global commands synced.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GLOBAL CMD ERROR] {ex}");
        }
    }

    // ====================================================
    // INTERACTION HANDLERS
    // ====================================================
    private static async Task InteractionCreated(SocketInteraction interaction)
    {
        try
        {
            var ctx = new SocketInteractionContext(Client, interaction);
            await Service.ExecuteCommandAsync(ctx, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CMD ERROR] {ex}");
        }
    }

    private static async Task SlashCommandResulted(
        SlashCommandInfo info, IInteractionContext ctx, IResult res)
    {
        if (!res.IsSuccess)
            await ctx.Interaction.FollowupAsync($"❌ Error: {res.ErrorReason}", ephemeral: true);
        else
            Console.WriteLine($"[CMD] Executed /{info.Name}");
    }

    private static Task Log(LogMessage log)
    {
        Console.WriteLine($"{log.Severity}: {log.Source} {log.Message}");
        return Task.CompletedTask;
    }
}
