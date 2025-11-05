using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using DotNetEnv;
using Performance;
using System.Linq;

public static class Bot
{
    // ⚠️ Replace this with your actual "tubas" sticker ID
    private const ulong TubasStickerId = 1435403416733225174;
    private const ulong RozkminkaStickerId = 1435646701137428511;

    public static readonly DiscordSocketClient Client = new(new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
    });

    private static InteractionService Service;
    private static readonly string Token = "MTQzNTM0NTIyNTU1MDkyMTczOQ.GPq8Jr.CwNZV7YZ5b7KYHynYz3NKOcksKgzrzMs0R6Eto";
    private static Timer timer;

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

        if (message.Author is SocketGuildUser user)
        {
            string contentLower = message.Content.ToLowerInvariant();

            if (contentLower.Contains("xddd"))
                await HandleXdddDetection(message, user);

            if (contentLower.Contains("tubas"))
                await HandleTubasDetection(message, user);
            
            if (contentLower.Contains("co"))
                await HandleRozkminkaDetection(message, user);
        }
    }

    private static async Task HandleXdddDetection(SocketMessage message, SocketGuildUser user)
    {
        Console.WriteLine($"[XDDD DETECTED] from {user.Username} in #{message.Channel.Name}");

        var kekwEmoji = Client.Guilds.SelectMany(g => g.Emotes)
            .FirstOrDefault(e => e.Name.Equals("kekw", StringComparison.OrdinalIgnoreCase));

        if (kekwEmoji != null)
        {
            string emojiString = kekwEmoji.ToString();
            if (message.Channel is SocketTextChannel textChannel)
            {
                var botUser = textChannel.Guild.CurrentUser;
                if (!botUser.GetPermissions(textChannel).SendMessages)
                {
                    Console.WriteLine("[XDDD ERROR] No permission to send messages.");
                    return;
                }

                await message.Channel.SendMessageAsync(emojiString);
                Console.WriteLine($"[XDDD SUCCESS] Sent {emojiString}");
            }
        }
        else
        {
            Console.WriteLine("[XDDD WARNING] Custom emoji 'kekw' not found.");
        }
    }

    private static async Task HandleTubasDetection(SocketMessage message, SocketGuildUser user)
    {
        Console.WriteLine($"[TUBAS DETECTED] from {user.Username} in #{message.Channel.Name}");

        if (message.Channel is not SocketTextChannel textChannel) return;

        var botUser = textChannel.Guild.CurrentUser;
        if (!botUser.GetPermissions(textChannel).SendMessages)
        {
            Console.WriteLine("[TUBAS ERROR] No permission to send messages.");
            return;
        }

    private static async Task HandleRozkminkaDetection(SocketMessage message, SocketGuildUser user)
    {
        Console.WriteLine($"[ROZKMINKA DETECTED] from {user.Username} in #{message.Channel.Name}");

        if (message.Channel is not SocketTextChannel textChannel) return;

        var botUser = textChannel.Guild.CurrentUser;
        if (!botUser.GetPermissions(textChannel).SendMessages)
        {
            Console.WriteLine("[ROZKMINKA ERROR] No permission to send messages.");
            return;
        }
        
        try
        {
            // ✅ FIXED: Fetch sticker object instead of using stickerIds
            var sticker = textChannel.Guild.Stickers.FirstOrDefault(s => s.Id == TubasStickerId);
            if (sticker != null)
            {
                await textChannel.SendMessageAsync(stickers: new[] { sticker });
                Console.WriteLine($"[TUBAS SUCCESS] Sent sticker with ID: {TubasStickerId}.");
            }
            else
            {
                Console.WriteLine($"[TUBAS ERROR] Sticker with ID {TubasStickerId} not found in this guild.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TUBAS ERROR] Error sending sticker: {ex.Message}");
        }
    }
    
    private static async Task HandleRozkminkaDetection(SocketMessage message, SocketGuildUser user)
    {
        Console.WriteLine($"[ROZKMINKA DETECTED] from {user.Username} in #{message.Channel.Name}");

        if (message.Channel is not SocketTextChannel textChannel) return;

        var botUser = textChannel.Guild.CurrentUser;
        if (!botUser.GetPermissions(textChannel).SendMessages)
        {
            Console.WriteLine("[ROZKMINKA ERROR] No permission to send messages.");
            return;
        }

        try
        {
            // ✅ FIXED: Fetch sticker object instead of using stickerIds
            var sticker = textChannel.Guild.Stickers.FirstOrDefault(s => s.Id == RozkminkaStickerId);
            if (sticker != null)
            {
                await textChannel.SendMessageAsync(stickers: new[] { sticker });
                Console.WriteLine($"[ROZKMINKA SUCCESS] Sent sticker with ID: {RozkminkaStickerId}.");
            }
            else
            {
                Console.WriteLine($"[ROZKMINKA ERROR] Sticker with ID {RozkminkaStickerId} not found in this guild.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ROZKMINKA ERROR] Error sending sticker: {ex.Message}");
        }
    }
    
private static async Task Ready()
{
    try
    {
        Service = new(Client, new InteractionServiceConfig
        {
            UseCompiledLambda = true,
            ThrowOnError = true
        });

        await Service.AddModulesAsync(Assembly.GetEntryAssembly(), null);
        await Service.RegisterCommandsGloballyAsync();

        Client.InteractionCreated += InteractionCreated;
        Service.SlashCommandExecuted += SlashCommandResulted;

        Console.WriteLine($"Bot is ready! Connected to {Client.Guilds.Count} guild(s).");

        // Initial presence: 🎮 Playing 777 Slots
        await Client.SetGameAsync("777 Slots", type: ActivityType.Playing);

        // Rotating between custom statuses and "playing" state
        string[] statuses = { "No Siemano!", "Ale kto pytał?", "Ale sigiemki tutaj" };
        int index = 0;
        bool showGame = false;

        timer = new Timer(async _ =>
        {
            if (Client.ConnectionState != ConnectionState.Connected)
                return;

            try
            {
                if (showGame)
                {
                    // 🎮 Show "Playing 777 Slots"
                    await Client.SetGameAsync("777 Slots", type: ActivityType.Playing);
                }
                else
                {
                    // 💬 Show custom status message
                    await Client.SetCustomStatusAsync(statuses[index]);
                    index = (index + 1) % statuses.Length;
                }

                showGame = !showGame; // Alternate between game and custom status
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[STATUS ERROR] {ex.Message}");
            }

        }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20)); // every 20 seconds
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
    }
}

    private static async Task InteractionCreated(SocketInteraction interaction)
    {
        try
        {
            SocketInteractionContext ctx = new(Client, interaction);
            await Service.ExecuteCommandAsync(ctx, null);
        }
        catch
        {
            if (interaction.Type == InteractionType.ApplicationCommand)
            {
                await interaction.GetOriginalResponseAsync().ContinueWith(async msg => await msg.Result.DeleteAsync());
            }
        }
    }

    private static async Task SlashCommandResulted(SlashCommandInfo info, IInteractionContext ctx, IResult res)
    {
        if (!res.IsSuccess)
        {
            await ctx.Interaction.FollowupAsync($"❌ Error: {res.ErrorReason}", ephemeral: true);
        }
        else
        {
            var cpuUsage = await Stats.GetCpuUsageForProcess();
            var ramUsage = Stats.GetRamUsageForProcess();
            Console.WriteLine($"{DateTime.Now:dd/MM. H:mm:ss} | CPU: {cpuUsage}% | RAM: {ramUsage}% | Command: {info.Name}");
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







