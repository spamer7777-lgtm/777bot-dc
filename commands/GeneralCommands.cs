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

        // 🎲 NEW: Bet Command
        [SlashCommand("bet", "Postaw zakład i spróbuj podwoić swoje kredyty!")]
        public async Task Bet(
            [Summary("amount", "Kwota, którą chcesz postawić.")] int amount)
        {
            if (amount <= 0)
            {
                await RespondAsync("⚠️ Podaj kwotę większą niż 0.", ephemeral: true);
                return;
            }

            var user = UserDataManager.GetUser(Context.User.Id);
            if (user.Credits < amount)
            {
                await RespondAsync($"🚫 Nie masz wystarczająco kredytów! Masz tylko {user.Credits}.", ephemeral: true);
                return;
            }

            var rand = new Random();
            bool win = rand.NextDouble() < 0.5; // 50% szansy na wygraną

            if (win)
            {
                UserDataManager.AddCredits(Context.User.Id, amount);
                await RespondAsync($"🎉 Wygrałeś/aś! Twoje **{amount}** kredytów zostało podwojone! 💰 Nowy balans: **{UserDataManager.GetUser(Context.User.Id).Credits}**");
            }
            else
            {
                UserDataManager.RemoveCredits(Context.User.Id, amount);
                await RespondAsync($"💀 Przegrałeś/aś **{amount}** kredytów! 😢 Aktualny balans: **{UserDataManager.GetUser(Context.User.Id).Credits}**");
            }
        }

        // 🏆 NEW: Leaderboard Command
        [SlashCommand("leaderboard", "Zobacz top 10 najbogatszych graczy!")]
        public async Task Leaderboard()
        {
            var topUsers = UserDataManager.GetTopUsers(10);
            if (topUsers == null || topUsers.Count == 0)
            {
                await RespondAsync("📉 Brak danych o użytkownikach.");
                return;
            }

            var desc = string.Join("\n", topUsers.Select((u, i) =>
                $"**#{i + 1}** <@{u.UserId}> — 💰 {u.Credits} kredytów"));

            var embed = new EmbedBuilder()
                .WithTitle("🏆 Tablica Najbogatszych 🏆")
                .WithDescription(desc)
                .WithColor(Color.Gold)
                .WithFooter("Czy uda ci się wejść do TOP 10?")
                .Build();

            await RespondAsync(embed: embed);
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
