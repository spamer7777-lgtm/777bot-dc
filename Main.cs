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

// ✅ WYCENA: nowe usingi (Twoje nowe klasy)
using _777bot;

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

    // =========================================================
    // ✅ WYCENA: globalne serwisy + pending
    // =========================================================
    public static PriceCatalog PriceCatalog { get; private set; } = new();
    public static VehicleMongoStore VehicleStore { get; private set; } = null!;
    public static ValuationService ValuationService { get; private set; } = null!;
    public static Dictionary<ulong, PendingWycenaState> PendingWycena { get; private set; } = new();

    public static async Task Main()
    {
        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        if (string.IsNullOrEmpty(token))
            throw new ArgumentException("DISCORD_TOKEN is not set!");

        // ✅ WYCENA: init (nie dotyka reszty)
        try
        {
            PriceCatalog = PriceCatalogLoader.LoadFromDataFolder(System.IO.Path.Combine(AppContext.BaseDirectory, "data"));
            VehicleStore = new VehicleMongoStore();
            ValuationService = new ValuationService(PriceCatalog, VehicleStore);
            Console.WriteLine("✅ Wycena: załadowano cenniki + Mongo gotowe.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WYCENA INIT ERROR] {ex}");
        }

        Client.Ready += Ready;
        Client.Log += Log;
        Client.MessageReceived += MessageReceivedHandler;

        // --- FIXED: Button handler registered here (SAFE) ---
        Client.ButtonExecuted += HandleButtonSafeWrapper;

        await Client.LoginAsync(TokenType.Bot, token);
        await Client.StartAsync();

        Console.WriteLine("✅ Bot + HTTP API running.");
        await Task.Delay(-1);
    }

    // ===============================================
    // BUTTON WRAPPER — prevents crashes
    // ===============================================
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

    // ===============================================
    // MESSAGE CREDITS
    // ===============================================
    private static async Task MessageReceivedHandler(SocketMessage message)
    {
        if (message.Author.Id == Client.CurrentUser.Id) return;
        if (message.Author is not SocketGuildUser user) return;
        if (message.Attachments.Any()) return;

        // =========================================================
        // ✅ WYCENA: obsługa "wklejki" i "podaj ceny limitowane"
        // =========================================================
        if (PendingWycena.TryGetValue(user.Id, out var pending))
        {
            // timeout
            if (DateTime.UtcNow > pending.ExpiresAtUtc)
            {
                PendingWycena.Remove(user.Id);
                await message.Channel.SendMessageAsync($"⏱️ {user.Mention} timeout. Zrób ponownie `/wycena vuid:{pending.Vuid}`.");
                return;
            }

            // tylko na tym samym kanale
            if (message.Channel.Id != pending.ChannelId) return;

            // 1) czekamy na wklejkę karty pojazdu
            if (pending.Kind == PendingKind.WaitingForVehiclePaste)
            {
                if (VehicleStore == null || ValuationService == null)
                {
                    PendingWycena.Remove(user.Id);
                    await message.Channel.SendMessageAsync($"❌ {user.Mention} wycena nie jest zainicjalizowana (sprawdź logi/env Mongo).");
                    return;
                }

                if (!VehicleCardParser.TryParse(message.Content, out var card, out var err))
                {
                    await message.Channel.SendMessageAsync($"❌ {user.Mention} Nie umiem tego sparsować: **{err}**\nWklej pełną kartę pojazdu (VUID/Model/Silnik/Tuning...).");
                    return;
                }

                if (card.Vuid != pending.Vuid)
                {
                    await message.Channel.SendMessageAsync($"❌ {user.Mention} Wklejka ma VUID **{card.Vuid}**, a czekam na **{pending.Vuid}**.");
                    return;
                }

                await VehicleStore.UpsertVehicleAsync(card);

                // jeśli limitowane/unikatowe i brak ceny w DB -> poproś o ceny
                var missingSpecial = await GetMissingSpecialColorsAsync(card);
                if (missingSpecial.Count > 0)
                {
                    pending.Kind = PendingKind.WaitingForSpecialColorPrices;
                    pending.MissingSpecialColors = missingSpecial;
                    pending.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10);
                    PendingWycena[user.Id] = pending;

                    await message.Channel.SendMessageAsync(BuildSpecialColorPricePrompt(user, pending));
                    return;
                }

                var result = await ValuationService.EvaluateAsync(card);
                PendingWycena.Remove(user.Id);
                await message.Channel.SendMessageAsync(embed: result.BuildEmbed(pending.Vuid, card));
                return;
            }

            // 2) czekamy na ceny limitowane/unikatowe (wpisywane przez użytkownika)
            if (pending.Kind == PendingKind.WaitingForSpecialColorPrices)
            {
                if (VehicleStore == null || ValuationService == null)
                {
                    PendingWycena.Remove(user.Id);
                    await message.Channel.SendMessageAsync($"❌ {user.Mention} wycena nie jest zainicjalizowana (sprawdź logi/env Mongo).");
                    return;
                }

                var parsed = ParseSpecialColorPriceReply(message.Content);

                // aplikujemy ceny: jeśli user poda "licznik=..." to ustawiamy dla wszystkich brakujących liczników itd.
                var stillMissing = new List<(SpecialColorType type, string name, string rarity)>();

                foreach (var need in pending.MissingSpecialColors)
                {
                    if (parsed.TryGetValue(need.type, out var price))
                    {
                        await VehicleStore.UpsertSpecialColorPriceAsync(need.type, need.name, need.rarity, price, user.Id);
                    }
                    else
                    {
                        stillMissing.Add(need);
                    }
                }

                if (stillMissing.Count > 0)
                {
                    pending.MissingSpecialColors = stillMissing;
                    PendingWycena[user.Id] = pending;

                    await message.Channel.SendMessageAsync(
                        $"❌ {user.Mention} Nadal brakuje cen dla:\n" +
                        string.Join("\n", stillMissing.Select(x => $"- {(x.type == SpecialColorType.Dashboard ? "licznik" : "swiatla")}: {x.name} - {x.rarity}")) +
                        "\nPodaj w formie:\n`licznik=35000`\n`swiatla=55000`"
                    );
                    return;
                }

                var card = await VehicleStore.GetVehicleAsync(pending.Vuid);
                if (card == null)
                {
                    PendingWycena.Remove(user.Id);
                    await message.Channel.SendMessageAsync($"❌ {user.Mention} Nie mogę znaleźć zapisanych danych VUID {pending.Vuid} po zapisaniu cen.");
                    return;
                }

                var result2 = await ValuationService.EvaluateAsync(card);
                PendingWycena.Remove(user.Id);
                await message.Channel.SendMessageAsync(embed: result2.BuildEmbed(pending.Vuid, card));
                return;
            }
        }

        // =========================================================
        // Twoja logika triggerów (bez zmian)
        // =========================================================
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

    // ===============================================
    // REACTION TRIGGERS (unchanged)
    // ===============================================
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

    // ===============================================
    // READY — FIXED (NO CRASH)
    // ===============================================
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
            await Service.RegisterCommandsGloballyAsync();

            Client.InteractionCreated += InteractionCreated;
            Service.SlashCommandExecuted += SlashCommandResulted;

            Console.WriteLine($"✅ Ready! Loaded {Service.Modules.Count} command modules.");

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
            Console.WriteLine($"[CMD] {info.Name}");
    }

    private static Task Log(LogMessage log)
    {
        Console.WriteLine($"{log.Severity}: {log.Source} {log.Message}");
        return Task.CompletedTask;
    }

    // =========================================================
    // ✅ WYCENA: helpery (tylko to, co potrzebne)
    // =========================================================
    private static async Task<List<(SpecialColorType type, string name, string rarity)>> GetMissingSpecialColorsAsync(VehicleCard card)
    {
        var list = new List<(SpecialColorType type, string name, string rarity)>();

        // lights
        var (lName, lRarity) = VehicleCardParser.ParseColorWithRarity(card.LightsColorRaw);
        if (!string.IsNullOrWhiteSpace(lName) &&
            (lRarity.Equals("Limitowane", StringComparison.OrdinalIgnoreCase) || lRarity.Equals("Unikatowe", StringComparison.OrdinalIgnoreCase)))
        {
            var p = await VehicleStore.GetSpecialColorPriceAsync(SpecialColorType.Lights, lName, lRarity);
            if (!p.HasValue) list.Add((SpecialColorType.Lights, lName, lRarity));
        }

        // dashboard
        var (dName, dRarity) = VehicleCardParser.ParseColorWithRarity(card.DashboardColorRaw);
        if (!string.IsNullOrWhiteSpace(dName) &&
            (dRarity.Equals("Limitowane", StringComparison.OrdinalIgnoreCase) || dRarity.Equals("Unikatowe", StringComparison.OrdinalIgnoreCase)))
        {
            var p = await VehicleStore.GetSpecialColorPriceAsync(SpecialColorType.Dashboard, dName, dRarity);
            if (!p.HasValue) list.Add((SpecialColorType.Dashboard, dName, dRarity));
        }

        // unique
        return list
            .GroupBy(x => (x.type, TextNorm.NormalizeKey(x.name), TextNorm.NormalizeKey(x.rarity)))
            .Select(g => g.First())
            .ToList();
    }

    private static string BuildSpecialColorPricePrompt(SocketGuildUser user, PendingWycenaState pending)
    {
        var lines = pending.MissingSpecialColors.Select(x =>
            $"- {(x.type == SpecialColorType.Dashboard ? "licznik" : "swiatla")}: {x.name} - {x.rarity}");

        return $"🧾 {user.Mention} Podaj ceny dla limitowanych/unikatowych kolorów (bot zapamięta):\n" +
               string.Join("\n", lines) +
               "\n\nPodaj w wiadomości np.:\n`licznik=35000`\n`swiatla=55000`\n(możesz podać jedną albo dwie linie)";
    }

    private static Dictionary<SpecialColorType, long> ParseSpecialColorPriceReply(string content)
    {
        // user może podać:
        // licznik=35000
        // swiatla=55000
        // (lub ':' zamiast '=')
        var dict = new Dictionary<SpecialColorType, long>();

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(new[] { '=', ':' }, 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;

            var left = parts[0].Trim().ToLowerInvariant();
            var right = parts[1].Trim()
                .Replace("$", "")
                .Replace(" ", "")
                .Replace(",", "");

            if (!long.TryParse(right, out var price)) continue;

            if (left.Contains("licznik") || left.Contains("dashboard"))
                dict[SpecialColorType.Dashboard] = price;
            else if (left.Contains("swiat") || left.Contains("świat") || left.Contains("lights"))
                dict[SpecialColorType.Lights] = price;
        }

        return dict;
    }
}
