using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Commands
{
    public class NoGroup : InteractionModuleBase<SocketInteractionContext>
    {
        public static Dictionary<ulong, int> RouletteStakes = new();

        private Embed Error(string msg) =>
            new EmbedBuilder().WithTitle("❌ Błąd").WithDescription(msg).WithColor(Color.Red)
            .WithCurrentTimestamp().WithFooter($"Wywołano przez {Context.User.Username}", Context.User.GetAvatarUrl()).Build();

        private Embed Loading(string msg) =>
            new EmbedBuilder().WithDescription(msg).WithColor(Color.DarkGrey)
            .WithCurrentTimestamp().Build();

        private EmbedBuilder BaseEmbed(string title, Color color)
        {
            return new EmbedBuilder()
                .WithTitle(title)
                .WithColor(color)
                .WithThumbnailUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                .WithFooter($"Wywołano przez {Context.User.Username}", Context.User.GetAvatarUrl())
                .WithCurrentTimestamp();
        }

        // PING
        [SlashCommand("ping", "Sprawdź opóźnienie.")]
        public async Task Ping()
        {
            var eb = BaseEmbed("🏓 Pong!", Color.Green);
            eb.AddField("📡 Opóźnienie:", $"**{Bot.Client.Latency} ms**", true);
            eb.WithDescription("Wszystko działa prawidłowo!");
            await RespondAsync(embed: eb.Build());
        }

        // HI
        [SlashCommand("hi", "Powiedz siemano.")]
        public async Task Hi(IUser user)
        {
            var eb = BaseEmbed("👋 Siemano!", Color.Gold);
            eb.WithDescription($"{user.Mention}, witam Cię serdecznie!");
            await RespondAsync(embed: eb.Build());
        }

        // BALANCE
        [SlashCommand("balance", "Sprawdź swój balans.")]
        public async Task Balance()
        {
            var data = await UserDataManager.GetUserAsync(Context.User.Id);
            var eb = BaseEmbed("💰 Twój balans", Color.Gold);
            eb.AddField("Aktualne kredyty:", $"**{data.Credits}** 💳");
            await RespondAsync(embed: eb.Build());
        }

        // SLOTS
        [SlashCommand("slots", "Jednoręki bandyta.")]
        public async Task Slots(int amount = 10)
        {
            if (amount <= 0) { await RespondAsync(embed: Error("Kwota musi być większa niż 0."), ephemeral: true); return; }

            var data = await UserDataManager.GetUserAsync(Context.User.Id);
            if (data.Credits < amount) { await RespondAsync(embed: Error("Masz za mało kredytów."), ephemeral: true); return; }

            await DeferAsync();
            await UserDataManager.RemoveCreditsAsync(Context.User.Id, amount);

            string[] icons = { "🍒", "🍋", "🍉", "💎", "7️⃣" };
            var rand = new Random();

            var msg = await FollowupAsync(embed: Loading("🎰 Kręcimy.")) as IUserMessage;

            for (int i = 0; i < 5; i++)
            {
                string roll = string.Join(" ", Enumerable.Range(0, 3).Select(_ => icons[rand.Next(icons.Length)]));
                await msg.ModifyAsync(m => m.Embed = Loading($"🎰 {roll}\nKręcimy."));
                await Task.Delay(200);
            }

            string[] final = Enumerable.Range(0, 3).Select(_ => icons[rand.Next(icons.Length)]).ToArray();
            bool win = final.All(x => x == final[0]);
            int reward = win ? amount * 5 : 0;

            if (win) await UserDataManager.AddCreditsAsync(Context.User.Id, reward);

            var eb = BaseEmbed("🎰 Wynik jednorękiego bandyty", win ? Color.Gold : Color.DarkRed);
            eb.WithDescription($"**{final[0]} {final[1]} {final[2]}**");
            eb.AddField(win ? "🎉 Wygrana!" : "💀 Przegrana!",
                        win ? $"Wygrałeś **{reward}** kredytów!" : $"Straciłeś **{amount}** kredytów.");
            eb.WithFooter($"Nowy balans: {(await UserDataManager.GetUserAsync(Context.User.Id)).Credits}",
                          Context.User.GetAvatarUrl());

            await msg.ModifyAsync(m => m.Embed = eb.Build());
        }

        // LEADERBOARD
        [SlashCommand("leaderboard", "Top 10 graczy.")]
        public async Task Leaderboard()
        {
            await DeferAsync();

            var list = await UserDataManager.GetTopUsersLeaderboardAsync(10);

            if (list.Count == 0)
            {
                await FollowupAsync(embed: Error("Brak danych."));
                return;
            }

            var eb = BaseEmbed("🏆 TOP 10 — Ranking kredytów", Color.Gold);

            string Medal(int i) =>
                i == 0 ? "🥇" :
                i == 1 ? "🥈" :
                i == 2 ? "🥉" : "▪";

            string desc = string.Join("\n",
                list.Select((u, i) =>
                    $"{Medal(i)} **#{i + 1}** — <@{u.UserId}>\n💰 Kredyty: **{u.Credits}**"));

            eb.WithDescription(desc);

            await FollowupAsync(embed: eb.Build());
        }

        // DAILY
        [SlashCommand("dzienne", "Dzienna nagroda.")]
        public async Task Daily()
        {
            await DeferAsync();

            ulong uid = Context.User.Id;

            if (!await UserDataManager.CanClaimDailyAsync(uid))
            {
                var remain = await UserDataManager.GetDailyCooldownRemainingAsync(uid);
                await FollowupAsync(embed: Error($"Spróbuj za {remain.Hours}h {remain.Minutes}m."));
                return;
            }

            int reward = new Random().Next(100, 251);

            await UserDataManager.AddCreditsAsync(uid, reward);
            await UserDataManager.SetDailyClaimAsync(uid);

            var data = await UserDataManager.GetUserAsync(uid);

            var eb = BaseEmbed("🎁 Nagroda dzienna", Color.Green);
            eb.WithDescription("Dziękujemy za codzienną aktywność!");
            eb.AddField("Nagroda:", $"✨ Otrzymujesz **{reward}** kredytów!", true);
            eb.AddField("Nowy balans:", $"💰 **{data.Credits}**", true);

            await FollowupAsync(embed: eb.Build());
        }

        // ADMIN GRANT
        [SlashCommand("grantcredits", "Dodaj kredyty (admin).")]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        public async Task GrantCredits(IUser target, int amount)
        {
            if (amount <= 0)
            {
                await RespondAsync(embed: Error("Kwota musi być > 0."), ephemeral: true);
                return;
            }

            await UserDataManager.AddCreditsAsync(target.Id, amount);
            var data = await UserDataManager.GetUserAsync(target.Id);

            var eb = BaseEmbed("🛠 ADMIN", Color.Orange);
            eb.AddField("Użytkownik:", target.Mention, true);
            eb.AddField("Dodano:", $"**{amount}** kredytów", true);
            eb.AddField("Nowy balans:", $"**{data.Credits}**", true);

            await RespondAsync(embed: eb.Build());
        }

        // RULETKA — COMMAND
        [SlashCommand("ruletka", "Postaw zakład na kolor.")]
        public async Task Ruletka(int stawka)
        {
            if (stawka <= 0) { await RespondAsync(embed: Error("Kwota musi być > 0."), ephemeral: true); return; }

            var data = await UserDataManager.GetUserAsync(Context.User.Id);
            if (data.Credits < stawka) { await RespondAsync(embed: Error("Za mało kredytów!"), ephemeral: true); return; }

            RouletteStakes[Context.User.Id] = stawka;

            var buttons = new ComponentBuilder()
                .WithButton("🔴 Czerwony", "roulette_red", ButtonStyle.Danger)
                .WithButton("⚫ Czarny", "roulette_black", ButtonStyle.Secondary)
                .WithButton("🟩 Zielony (0)", "roulette_green", ButtonStyle.Success);

            var eb = BaseEmbed("🎡 Ruletka", Color.Blue);
            eb.WithDescription($"Stawiasz **{stawka}** kredytów.\nWybierz kolor poniżej.");

            await RespondAsync(embed: eb.Build(), components: buttons.Build());
        }

        // BUTTON HANDLER
        public static async Task HandleRouletteButtonsStatic(SocketMessageComponent component)
        {
            if (!component.Data.CustomId.StartsWith("roulette_")) return;

            if (!component.HasResponded)
                await component.DeferAsync();

            ulong uid = component.User.Id;

            if (!RouletteStakes.ContainsKey(uid))
            {
                await component.FollowupAsync(embed: new EmbedBuilder().WithTitle("❌ Błąd").WithDescription("Nie postawiłeś zakładu!").WithColor(Color.Red).Build());
                return;
            }

            int stake = RouletteStakes[uid];
            string choice = component.Data.CustomId.Replace("roulette_", "");

            var rand = new Random();
            int roll = rand.Next(0, 37);

            string resultColor =
                roll == 0 ? "green" :
                roll % 2 == 0 ? "black" :
                "red";

            bool win = choice switch
            {
                "green" => resultColor == "green",
                "red" => resultColor == "red",
                "black" => resultColor == "black",
                _ => false
            };

            if (win)
                await UserDataManager.AddCreditsAsync(uid, stake);
            else
                await UserDataManager.RemoveCreditsAsync(uid, stake);

            int newBal = (await UserDataManager.GetUserAsync(uid)).Credits;

            var eb = new EmbedBuilder()
                .WithTitle("🎡 Wynik ruletki")
                .WithColor(win ? Color.Green : Color.DarkRed)
                .WithDescription($"Wylosowano: **{roll}** ({resultColor})")
                .AddField(win ? "🎉 Wygrana!" : "💀 Przegrana!",
                    win ? $"Zyskałeś **{stake}** kredytów" : $"Straciłeś **{stake}** kredytów")
                .AddField("Nowy balans:", $"**{newBal}**")
                .WithCurrentTimestamp();

            await component.FollowupAsync(embed: eb.Build());
            RouletteStakes.Remove(uid);
        }
    }
}
