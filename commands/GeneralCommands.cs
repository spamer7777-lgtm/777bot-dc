using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Commands
{
    public class NoGroup : InteractionModuleBase<SocketInteractionContext>
    {
        private static readonly HttpClient http = new();

        [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
        [IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
        [SlashCommand("ping", "Zobacz ping bota.")]
        public async Task Ping()
        {
            await RespondAsync(text: $"🏓 Pong! Opóźnienie klienta: **{Bot.Client.Latency}** ms.");
        }

        [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
        [IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
        [SlashCommand("hi", "Powiedz Siemano!")]
        public async Task Hi([Summary("user", "Użytkownik, do którego chcesz powiedzieć siemano.")] IUser user)
        {
            await RespondAsync(text: $"👋 HEEEJ! {user.Mention}!");
        }

        [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
        [IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
        [SlashCommand("balance", "Sprawdź swój aktualny balans kredytów.")]
        public async Task Balance()
        {
            var user = UserDataManager.GetUser(Context.User.Id);
            var embed = new EmbedBuilder()
                .WithTitle($"💰 Balans użytkownika: {Context.User.Username}")
                .WithDescription($"Masz **{user.Credits}** kredytów.")
                .WithColor(Color.Gold)
                .Build();

            await RespondAsync(embed: embed);
        }

        [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
        [IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
        [SlashCommand("slots", "Sprawdź swoje szczęście")]
        [DefaultMemberPermissions(GuildPermission.SendMessages)] // Dostępne dla wszystkich użytkowników
        public async Task Slots()
        {
            const int cost = 10;
            const int reward = 50;

            var user = UserDataManager.GetUser(Context.User.Id);

            if (user.Credits < cost)
            {
                await RespondAsync($"🚫 Potrzebujesz {cost} kredytów, żeby zagrać. Aktualnie masz ich: {user.Credits}.");
                return;
            }

            await DeferAsync();

            UserDataManager.RemoveCredits(Context.User.Id, cost);

            string[] icons = { "🍒", "🍋", "🍉", "💎", "7️⃣" };
            string[] effects = { "🔔", "✨", "💥", "🎵", "⭐", "⚡" };
            var rand = new Random();

            var embed = new EmbedBuilder()
                .WithTitle("🎰 777 Slots 🎰")
                .WithDescription("[⬜][⬜][⬜] Kręcimy...")
                .WithColor(Color.DarkGrey)
                .WithFooter($"Twój nowy balans: {user.Credits} kredytów")
                .Build();

            var msg = await FollowupAsync(embed: embed, ephemeral: false) as IUserMessage;
            if (msg == null) return;

            // Animacja bębnów
            for (int i = 0; i < 6; i++)
            {
                var spin = Enumerable.Range(0, 3)
                    .Select(_ => icons[rand.Next(icons.Length)])
                    .ToArray();

                var effect1 = effects[rand.Next(effects.Length)];
                var effect2 = effects[rand.Next(effects.Length)];

                embed = new EmbedBuilder()
                    .WithTitle($"{effect2} 🎰 777 Slots 🎰 {effect1}")
                    .WithDescription($"[{spin[0]}][{spin[1]}][{spin[2]}] Kręcimy...")
                    .WithColor(Color.DarkGrey)
                    .WithFooter($"Twój nowy balans: {UserDataManager.GetUser(Context.User.Id).Credits} kredytów")
                    .Build();

                await msg.ModifyAsync(m => m.Embed = embed);
                await Task.Delay(250);
            }

            // Wynik końcowy
            var finalResult = Enumerable.Range(0, 3)
                .Select(_ => icons[rand.Next(icons.Length)])
                .ToArray();

            bool win = finalResult.Distinct().Count() == 1;
            if (win) UserDataManager.AddCredits(Context.User.Id, reward);

            embed = new EmbedBuilder()
                .WithTitle("🎰 777 Slots 🎰")
                .WithDescription($"[{finalResult[0]}][{finalResult[1]}][{finalResult[2]}]\n" +
                                 (win ? $"💰 **JACKPOT! WYGRAŁEŚ/AŚ {reward} kredytów!**" :
                                        $"😢 Przegrałeś/aś {cost} kredytów. Następnym razem lepiej!"))
                .WithColor(win ? Color.Gold : Color.DarkGrey)
                .WithFooter($"Twój nowy balans: {UserDataManager.GetUser(Context.User.Id).Credits} kredytów")
                .Build();

            await msg.ModifyAsync(m => m.Embed = embed);
        }

[CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
[IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
[SlashCommand("odsluch", "Sprawdź kto aktualnie nadaje i ilu jest słuchaczy.")]
public async Task Odsłuch()
{
    await DeferAsync();

    try
    {
        string url = "https://radio.projectrpg.pl/statsv2";

        // 🔹 Tworzymy HttpRequest z nagłówkami jak przeglądarka
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.1 Safari/537.36");
        request.Headers.Add("Accept", "application/json, text/plain, */*");
        request.Headers.Add("Referer", "https://radio.projectrpg.pl/");
        request.Headers.Add("Origin", "https://radio.projectrpg.pl");
        request.Headers.Add("Accept-Language", "pl,en;q=0.9");
        request.Headers.Add("Connection", "keep-alive");

        var response = await Bot.Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            await FollowupAsync($"❌ Nie udało się pobrać danych z API. Kod błędu: {(int)response.StatusCode} {response.ReasonPhrase}", ephemeral: true);
            return;
        }

        string json = await response.Content.ReadAsStringAsync();

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        var live = root.GetProperty("live");
        bool isLive = live.GetProperty("is_live").GetBoolean();
        string streamer = isLive ? live.GetProperty("streamer_name").GetString() ?? "Nieznany" : "Offline";

        var listeners = root.GetProperty("listeners");
        int uniqueListeners = listeners.GetProperty("unique").GetInt32();
        int totalListeners = listeners.GetProperty("total").GetInt32();

        var listenUrl = root.GetProperty("station").GetProperty("listen_url").GetString() ?? "https://radio.projectrpg.pl";

        var embed = new EmbedBuilder()
            .WithTitle("📻 ProjectFM – Status")
            .WithDescription(isLive
                ? $"🎙️ **Na żywo:** `{streamer}`\n👥 **Unikalnych słuchaczy:** `{uniqueListeners}`\n🔊 **Łączna liczba słuchaczy:** `{totalListeners}`"
                : "🚫 Aktualnie nikt nie nadaje.")
            .AddField("🔗 Link do odsłuchu", $"[Kliknij, aby słuchać]({listenUrl})")
            .WithColor(isLive ? Color.Green : Color.Red)
            .WithFooter("Dane pochodzą z radio.projectrpg.pl")
            .WithCurrentTimestamp()
            .Build();

        await FollowupAsync(embed: embed, ephemeral: false);
    }
    catch (Exception ex)
    {
        await FollowupAsync($"⚠️ Błąd przy pobieraniu danych: {ex.Message}", ephemeral: true);
    }
}


        // 🛠️ Komenda administratora
        [SlashCommand("grantcredits", "Administrator: dodaj kredyty użytkownikowi (ukryta).")]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [CommandContextType(InteractionContextType.Guild)]
        [IntegrationType(ApplicationIntegrationType.GuildInstall)]
        public async Task GrantCredits(
            [Summary("user", "Użytkownik, któremu chcesz dodać kredyty.")] IUser target,
            [Summary("amount", "Liczba kredytów do dodania.")] int amount)
        {
            ulong ownerId = 299929951451217921; // Twój Discord ID

            if (Context.User.Id != ownerId && !((SocketGuildUser)Context.User).GuildPermissions.Administrator)
            {
                await RespondAsync("🚫 Nie masz uprawnień do użycia tej komendy.", ephemeral: true);
                return;
            }

            if (amount <= 0)
            {
                await RespondAsync("⚠️ Ilość musi być większa niż 0.", ephemeral: true);
                return;
            }

            UserDataManager.AddCredits(target.Id, amount);
            var newBalance = UserDataManager.GetUser(target.Id).Credits;

            await RespondAsync(
                $"✅ Dodano **{amount}** kredytów użytkownikowi {target.Mention}. Nowy balans: **{newBalance}** kredytów.",
                ephemeral: true
            );
        }
    }
}



