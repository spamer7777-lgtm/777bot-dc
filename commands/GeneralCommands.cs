using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Commands
{
    public class NoGroup : InteractionModuleBase<SocketInteractionContext>
    {
        // ===========================
        // GLOBAL STATE
        // ===========================
        private static readonly Dictionary<ulong, int> RouletteStakes = new();

        // Register roulette button handler ONCE
        public override void OnModuleBuilding(InteractionService commandService)
        {
            Bot.Client.ButtonExecuted += HandleRouletteButtons;
        }

        // ===========================
        // /ping
        // ===========================
        [SlashCommand("ping", "Zobacz ping bota.")]
        public async Task Ping()
        {
            await RespondAsync($"🏓 Pong! Ping: **{Bot.Client.Latency} ms**");
        }

        // ===========================
        // /hi
        // ===========================
        [SlashCommand("hi", "Powiedz Siemano!")]
        public async Task Hi(IUser user)
        {
            await RespondAsync($"👋 Siemano {user.Mention}!");
        }

        // ===========================
        // /balance
        // ===========================
        [SlashCommand("balance", "Sprawdź kredyty.")]
        public async Task Balance()
        {
            var data = await UserDataManager.GetUserAsync(Context.User.Id);

            var embed = new EmbedBuilder()
                .WithTitle($"💰 Balans: {Context.User.Username}")
                .WithDescription($"Masz **{data.Credits}** kredytów.")
                .WithColor(Color.Gold);

            await RespondAsync(embed: embed.Build());
        }

        // ===========================
        // /slots
        // ===========================
        [SlashCommand("slots", "Zagraj w 777 Slots")]
        public async Task Slots(int amount = 10)
        {
            if (amount <= 0)
                { await RespondAsync("⚠️ Stawka musi być większa niż 0.", ephemeral: true); return; }

            var user = await UserDataManager.GetUserAsync(Context.User.Id);
            if (user.Credits < amount)
                { await RespondAsync($"🚫 Masz tylko {user.Credits} kredytów!", ephemeral: true); return; }

            await DeferAsync();
            await UserDataManager.RemoveCreditsAsync(Context.User.Id, amount);

            string[] icons = { "🍒", "🍋", "🍉", "💎", "7️⃣" };
            var rand = new Random();
            var msg = await FollowupAsync("🎰 Kręcimy...") as IUserMessage;

            for (int i = 0; i < 6; i++)
            {
                var spin = Enumerable.Range(0, 3).Select(_ => icons[rand.Next(icons.Length)]).ToArray();
                await msg.ModifyAsync(m => m.Content = $"{spin[0]} {spin[1]} {spin[2]} – kręcimy...");
                await Task.Delay(200);
            }

            var final = Enumerable.Range(0, 3).Select(_ => icons[rand.Next(icons.Length)]).ToArray();
            bool win = final.Distinct().Count() == 1;
            int reward = win ? amount * 5 : 0;

            if (win)
                await UserDataManager.AddCreditsAsync(Context.User.Id, reward);

            await msg.ModifyAsync(m => m.Content =
                $"{final[0]} {final[1]} {final[2]}\n" +
                (win ? $"🎉 WYGRAŁEŚ **{reward}**!" : $"😢 Straciłeś {amount}"));
        }

        // ===========================
        // /ruletka (SAFE VERSION)
        // ===========================
        [SlashCommand("ruletka", "Postaw zakład na ruletkę!")]
        public async Task Ruletka(int stawka)
        {
            if (stawka <= 0)
                { await RespondAsync("⚠️ Stawka musi być > 0", ephemeral: true); return; }

            var user = await UserDataManager.GetUserAsync(Context.User.Id);
            if (user.Credits < stawka)
                { await RespondAsync($"🚫 Masz tylko {user.Credits} kredytów!", ephemeral: true); return; }

            RouletteStakes[Context.User.Id] = stawka;
            await UserDataManager.RemoveCreditsAsync(Context.User.Id, stawka);

            var components = new ComponentBuilder()
                .WithButton("🔴 Czerwony", "roulette_red", ButtonStyle.Danger)
                .WithButton("⚫ Czarny", "roulette_black", ButtonStyle.Secondary)
                .WithButton("🟩 Zielony (0)", "roulette_green", ButtonStyle.Success);

            var embed = new EmbedBuilder()
                .WithTitle("🎡 Ruletka")
                .WithDescription($"Stawka: **{stawka}**\nWybierz kolor ⬇️")
                .WithColor(Color.DarkTeal);

            await RespondAsync(embed: embed.Build(), components: components.Build());
        }

        // ===========================
        // GLOBAL BUTTON HANDLER
        // ===========================
        private async Task HandleRouletteButtons(SocketMessageComponent component)
        {
            if (!component.Data.CustomId.StartsWith("roulette_"))
                return;

            await component.DeferAsync();

            ulong uid = component.User.Id;

            if (!RouletteStakes.TryGetValue(uid, out int stawka))
            {
                await component.FollowupAsync("⚠️ Nie masz aktywnej ruletki!", ephemeral: true);
                return;
            }

            string choice = component.Data.CustomId.Replace("roulette_", "");

            var rand = new Random();
            int finalNumber = rand.Next(0, 37);

            string finalColor =
                finalNumber == 0 ? "green" :
                finalNumber % 2 == 0 ? "black" : "red";

            var msg = await component.FollowupAsync("🎡 Kula się kręci...") as IUserMessage;

            foreach (var n in Enumerable.Range(0, 12).Select(_ => rand.Next(0, 37)).Append(finalNumber))
            {
                string icon = n == 0 ? "🟩" : (n % 2 == 0 ? "⚫" : "🔴");
                await msg.ModifyAsync(m => m.Content = $"Kula: **{icon} {n}**");
                await Task.Delay(140);
            }

            bool win = choice == finalColor;

            int reward =
                finalColor == "green" ? stawka * 14 :
                win ? stawka * 2 : 0;

            if (win)
                await UserDataManager.AddCreditsAsync(uid, reward);

            RouletteStakes.Remove(uid);

            await msg.ModifyAsync(m => m.Content =
                $"🎯 Wypadło **{finalColor.ToUpper()} ({finalNumber})**!\n" +
                (win
                    ? $"🎉 WYGRAŁEŚ **{reward}**!"
                    : $"💀 Przegrałeś **{stawka}** kredytów."));
        }

        // ===========================
        // /bet  ← FIXED + INCLUDED
        // ===========================
        [SlashCommand("bet", "Postaw zakład i spróbuj podwoić swoje kredyty!")]
        public async Task Bet(int amount)
        {
            if (amount <= 0)
            {
                await RespondAsync("⚠️ Stawka musi być > 0", ephemeral: true);
                return;
            }

            var user = await UserDataManager.GetUserAsync(Context.User.Id);
            if (user.Credits < amount)
            {
                await RespondAsync($"🚫 Masz tylko {user.Credits} kredytów!", ephemeral: true);
                return;
            }

            bool win = new Random().NextDouble() < 0.5;
            int winAmount = amount;

            if (win)
                await UserDataManager.AddCreditsAsync(Context.User.Id, winAmount);
            else
                await UserDataManager.RemoveCreditsAsync(Context.User.Id, winAmount);

            int newBalance = (await UserDataManager.GetUserAsync(Context.User.Id)).Credits;

            var embed = new EmbedBuilder()
                .WithTitle(win ? "💰 WYGRAŁEŚ!" : "💀 PRZEGRAŁEŚ!")
                .WithDescription(win
                    ? $"Podwoiłeś stawkę i wygrałeś **{winAmount}** kredytów!\nNowy balans: **{newBalance}**"
                    : $"Straciłeś **{amount}** kredytów.\nNowy balans: **{newBalance}**")
                .WithColor(win ? Color.Gold : Color.Red)
                .Build();

            await RespondAsync(embed: embed);
        }

        // ===========================
        // /dzienne
        // ===========================
        [SlashCommand("dzienne", "Odbierz dzienną nagrodę.")]
        public async Task Daily()
        {
            await DeferAsync();

            ulong id = Context.User.Id;

            if (!await UserDataManager.CanClaimDailyAsync(id))
            {
                var rem = await UserDataManager.GetDailyCooldownRemainingAsync(id);
                await FollowupAsync($"⏳ Dostępne za {rem.Hours}h {rem.Minutes}m", ephemeral: true);
                return;
            }

            int reward = new Random().Next(100, 251);
            await UserDataManager.AddCreditsAsync(id, reward);
            await UserDataManager.SetDailyClaimAsync(id);

            int bal = (await UserDataManager.GetUserAsync(id)).Credits;

            await FollowupAsync($"🎁 Otrzymałeś **{reward}**!\nNowy balans: **{bal}**");
        }

        // ===========================
        // /leaderboard
        // ===========================
        [SlashCommand("leaderboard", "Top 10 graczy")]
        public async Task Leaderboard()
        {
            await DeferAsync();
            var list = await UserDataManager.GetTopUsersLeaderboardAsync(10);

            if (!list.Any())
            {
                await FollowupAsync("📉 Brak danych.");
                return;
            }

            string desc = string.Join("\n",
                list.Select((u, i) => $"**#{i + 1}** <@{u.UserId}> — **{u.Credits}** kredytów"));

            var embed = new EmbedBuilder()
                .WithTitle("🏆 Najbogatsi gracze")
                .WithDescription(desc)
                .WithColor(Color.Gold)
                .Build();

            await FollowupAsync(embed: embed);
        }

        // ===========================
        // /grantcredits
        // ===========================
        [SlashCommand("grantcredits", "Admin: daj kredyty użytkownikowi")]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        public async Task GrantCredits(IUser target, int amount)
        {
            if (amount <= 0)
            {
                await RespondAsync("⚠️ Ilość > 0!", ephemeral: true);
                return;
            }

            await UserDataManager.AddCreditsAsync(target.Id, amount);
            int bal = (await UserDataManager.GetUserAsync(target.Id)).Credits;

            await RespondAsync(
                $"✅ Dodano **{amount}** kredytów dla {target.Mention}.\nNowy balans: **{bal}**",
                ephemeral: true);
        }
    }
}
