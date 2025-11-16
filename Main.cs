using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Performance;
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
    private static readonly string Token = "MTQzNTM0NTIyNTU1MDkyMTczOQ.GPq8Jr.CwNZV7YZ5b7KYHynYz3NKOcksKgzrzMs0R6Eto";
    private static Timer timer;

    // 🟢 Nowy globalny HttpClient z poprawnymi nagłówkami
    public static readonly HttpClient Http = new HttpClient(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        UseProxy = true
    })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    // 🟢 Statyczny konstruktor — dodaje nagłówki
    static Bot()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DiscordBot/1.0");
        Http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // ------------------ MESSAGE CREDIT DROPS ------------------
    private static readonly Dictionary<ulong, DateTime> messageCreditCooldowns = new();
    private static readonly int creditCooldownSeconds = 60; // 1 minute cooldown
    private static readonly int creditAmountMin = 1;
    private static readonly int creditAmountMax = 5;

    public static async Task Main()
    {
        if (Token is null)
            throw new ArgumentException("Discord bot token not set properly.");

        Client.Ready += Ready;
        Client.Log += Log;
        Client.MessageReceived += MessageReceivedHandler;

        await Client.LoginAsync(TokenType.Bot, Token);
        await Client.StartAsync();
        await Task.Delay(Timeout.Infinite);
    }

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

        // ------------------ MESSAGE CREDIT DROP ------------------
        if (triggerCount == 0)
            await HandleMessageCredits(user);

        // ------------------ SPECIAL TRIGGERS ------------------
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
        // Check cooldown
        if (messageCreditCooldowns.TryGetValue(user.Id, out DateTime lastClaim))
        {
            if ((DateTime.UtcNow - lastClaim).TotalSeconds < creditCooldownSeconds)
                return; // still in cooldown
        }

        // Give random credits
        var rand = new Random();
        int reward = rand.Next(creditAmountMin, creditAmountMax + 1);

        await UserDataManager.AddCreditsAsync(user.Id, reward);

        messageCreditCooldowns[user.Id] = DateTime.UtcNow;

        var newBalance = (await UserDataManager.GetUserAsync(user.Id)).Credits;
        Console.WriteLine($"[CREDIT DROP] Gave {reward} credits to {user.Username}. New balance: {newBalance}");
    }

    private static async Task HandleXdddDetection(SocketMessage message, SocketGuildUser user)
    {
        Console.WriteLine($"[XDDD DETECTED] from {user.Username} in #{message.Channel.Name}");
        var kekwEmoji = Client.Guilds.SelectMany(g => g.Emotes)
            .FirstOrDefault(e => e.Name.Equals("kekw", StringComparison.OrdinalIgnoreCase));

        if (kekwEmoji != null && message.Channel is SocketTextChannel textChannel)
        {
            var botUser = textChannel.Guild.CurrentUser;
            if (!botUser.GetPermissions(textChannel).SendMessages) return;

            await message.Channel.SendMessageAsync(kekwEmoji.ToString());
            Console.WriteLine($"[XDDD SUCCESS] Sent {kekwEmoji}");
        }
    }

    private static async Task HandleTubasDetection(SocketMessage message, SocketGuildUser user)
    {
        Console.WriteLine($"[TUBAS DETECTED] from {user.Username} in #{message.Channel.Name}");
        if (message.Channel is not SocketTextChannel textChannel) return;
        var botUser = textChannel.Guild.CurrentUser;
        if (!botUser.GetPermissions(textChannel).SendMessages) return;

        try
        {
            var sticker = textChannel.Guild.Stickers.FirstOrDefault(s => s.Id == TubasStickerId);
            if (sticker != null)
                await textChannel.SendMessageAsync(stickers: new[] { sticker });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TUBAS ERROR] {ex.Message}");
        }
    }

    private static async Task HandleRozkminkaDetection(SocketMessage message, SocketGuildUser user)
    {
        Console.WriteLine($"[ROZKMINKA DETECTED] from {user.Username} in #{message.Channel.Name}");
        if (message.Channel is not SocketTextChannel textChannel) return;
        var botUser = textChannel.Guild.CurrentUser;
        if (!botUser.GetPermissions(textChannel).SendMessages) return;

        try
        {
            var sticker = textChannel.Guild.Stickers.FirstOrDefault(s => s.Id == RozkminkaStickerId);
            if (sticker != null)
                await textChannel.SendMessageAsync(stickers: new[] { sticker });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ROZKMINKA ERROR] {ex.Message}");
        }
    }

    private static async Task Ready()
    {
        Service = new(Client, new InteractionServiceConfig
        {
            UseCompiledLambda = true,
            ThrowOnError = true
        });

        await Service.AddModulesAsync(Assembly.GetEntryAssembly(), null);
        //ulong guildId = 1222629512056148008; // <-- replace with your Discord server ID
        //await Service.RegisterCommandsToGuildAsync(guildId);
        //await Client.Rest.DeleteAllGlobalCommandsAsync();
        await Service.RegisterCommandsGloballyAsync();
        Client.InteractionCreated += InteractionCreated;
        Service.SlashCommandExecuted += SlashCommandResulted;

        Console.WriteLine($"✅ Bot is ready! Loaded {Service.Modules.Count} command modules.");
        await Client.SetGameAsync("777 Slots", type: ActivityType.Playing);

        string[] statuses = { "No Siemano!", "Ale kto pytał?", "Ale sigiemki tutaj" };
        int index = 0;
        bool showGame = false;

        timer = new Timer(async _ =>
        {
            if (Client.ConnectionState != ConnectionState.Connected) return;
            try
            {
                if (showGame)
                    await Client.SetGameAsync("777 Slots", type: ActivityType.Playing);
                else
                    await Client.SetCustomStatusAsync(statuses[index]);

                index = (index + 1) % statuses.Length;
                showGame = !showGame;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[STATUS ERROR] {ex.Message}");
            }
        }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20));
    }

    private static async Task InteractionCreated(SocketInteraction interaction)
    {
        try
        {
            var ctx = new SocketInteractionContext(Client, interaction);
            await Service.ExecuteCommandAsync(ctx, null);
        }
        catch
        {
            if (interaction.Type == InteractionType.ApplicationCommand)
                await interaction.GetOriginalResponseAsync()
                    .ContinueWith(async msg => await msg.Result.DeleteAsync());
        }
    }

    private static async Task SlashCommandResulted(SlashCommandInfo info, IInteractionContext ctx, IResult res)
    {
        if (!res.IsSuccess)
            await ctx.Interaction.FollowupAsync($"❌ Error: {res.ErrorReason}", ephemeral: true);
        else
        {
            // Commented out Stats for now since it may not exist
            // var cpuUsage = await Stats.GetCpuUsageForProcess();
            // var ramUsage = Stats.GetRamUsageForProcess();
            Console.WriteLine($"{DateTime.Now:dd/MM. H:mm:ss} | Command: {info.Name}");
        }
    }

    private static Task Log(LogMessage logMessage)
    {
        Console.ForegroundColor = logMessage.Severity switch
        {
            LogSeverity.Critical => ConsoleColor.Red,
            LogSeverity.Debug => ConsoleColor.Blue,
            LogSeverity.Error => ConsoleColor.Yellow,
            LogSeverity.Info => ConsoleColor.Cyan,
            LogSeverity.Verbose => ConsoleColor.Green,
            LogSeverity.Warning => ConsoleColor.Magenta,
            _ => ConsoleColor.White,
        };
        Console.WriteLine($"{DateTime.Now:dd/MM. H:mm:ss} [{logMessage.Source}] {logMessage.Message}");
        Console.ResetColor();
        return Task.CompletedTask;
    }
}






