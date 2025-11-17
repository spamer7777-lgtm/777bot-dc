using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Commands
{
    public class NoGroup : InteractionModuleBase<SocketInteractionContext>
    {
        // Stores roulette stakes (user → stawka)
        public static Dictionary<ulong, int> RouletteStakes = new();

        // -----------------------------
        //       BASIC COMMANDS
        // -----------------------------
        
        [SlashCommand("ping", "Zobacz ping bota.")]
        public async Task Ping()
        {
            await RespondAsync($"🏓 Pong! Client latency: **{Bot.Client.Latency}**ms");
        }

        [SlashCommand("hi", "Powiedz siemano użytkownikowi.")]
        public async Task Hi(IUser user)
        {
            await RespondAsync($"👋 Siemano {user.Mention}!");
        }

        [SlashCommand("balance", "Sprawdź swój balans.")]
        public async Task Balance()
        {
            var data = await UserDataManager.GetUserAsync(Context.User.Id);

            var embed = new EmbedBuilder()
                .WithTitle("💰 Twój balans")
                .WithDescription($"Masz **{data.Credits}** kredytów.")
                .WithColor(Color.Gold)
                .Build();

            await RespondAsync(embed: embed);
        }

        // -----------------------------
        //            SLOTS
        // -----------------------------

        [SlashCommand("slots", "Zagraj w jednorękiego bandytę.")]
        public async Task Slots(int amount = 10)
        {
            if (amount <= 0)
            {
                await RespondAsync("Kwota musi być > 0", ephemeral: true);
                return;
            }

            var user = await UserDataManager.GetUserAsync(Context.User.Id);
            if (user.Credits < amount)
            {
                await RespondAsync("Nie masz tyle kredytów!", ephemeral: true);
                return;
            }

            await DeferAsync();
            await UserDataManager.RemoveCreditsAsync(Context.User.Id, amount);

            string[] icons = { "🍒", "🍋", "🍉", "💎", "7️⃣" };
            var rand = new Random();

            var msg = await FollowupAsync("Kręcimy...") as IUserMessage;

            for (int i = 0; i < 6; i++)
            {
                string roll = string.Join("", Enumerable.Range(0, 3).Select(_ => $"[{icons[rand.Next(icons.Length)]}]"));
                await msg.ModifyAsync(m => m.Content = $"{roll} Kręcimy...");
                await Task.Delay(200);
            }

            string[] final = Enumerable.Range(0, 3).Select(_ => icons[rand.Next(icons.Length)]).ToArray();

            bool win = final[0] == final[1] && final[1] == final[2];
            int reward = win ? amount * 5 : 0;

            if (win)
                await UserDataManager.AddCreditsAsync(Context.User.Id, reward);

            string resultMsg = $"🎰 **{string.Join("", final.Select(f => $"[{f}]"))}**\n" +
                               (win ? $"🎉 Wygrałeś **{reward}**!" : $"😢 Przegrałeś **{amount}**");

            await msg.ModifyAsync(m => m.Content = resultMsg);
        }

        // -----------------------------
        //         RULETKA
        // -----------------------------

        [SlashCommand("ruletka", "Postaw zakład na kolor.")]
        public async Task Ruletka(int stawka)
        {
            if (stawka <= 0)
            {
                await RespondAsync("Kwota musi być > 0", ephemeral: true);
                return;
            }

            var user = await UserDataManager.GetUserAsync(Context.User.Id);
            if (user.Credits < stawka)
            {
                await RespondAsync("Nie masz tyle kredytów!", ephemeral: true);
                return;
            }

            RouletteStakes[Context.User.Id] = stawka;

            var buttons = new ComponentBuilder()
                .WithButton("🔴 Czerwony", "roulette_red", ButtonStyle.Danger)
                .WithButton("⚫ Czarny", "roulette_black", ButtonStyle.Secondary)
                .WithButton("🟩 Zielony (0)", "roulette_green", ButtonStyle.Success);

            await RespondAsync(
                $"🎡 Postawiłeś **{stawka}**. Wybierz kolor:",
                components: buttons.Build()
            );
        }

        // STATIC handler registered in Main.cs
        public static async Task HandleRouletteButtonsStatic(SocketMessageComponent component)
        {
            if (!component.Data.CustomId.StartsWith("roulette_"))
                return;

            ulong userId = component.User.Id;

            if (!RouletteStakes.TryGetValue(userId, out int stawka))
            {
                await component.RespondAsync("Nie masz aktywnej ruletki!", ephemeral: true);
                return;
            }

            await component.DeferAsync();

            string choice = component.Data.CustomId.Replace("roulette_", "");

            var rand = new Random();
            int finalNumber = rand.Next(0, 37);

            string finalColor =
                finalNumber == 0 ? "green" :
                finalNumber % 2 == 0 ? "black" : "red";

            var msg = await component.FollowupAsync("🎡 Kręcimy...") as IUserMessage;

            foreach (int n in Enumerable.Range(0, 12).Select(_ => rand.Next(0, 37)).Append(finalNumber))
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
                await UserDataManager.AddCreditsAsync(userId, reward);
            else
                await UserDataManager.RemoveCreditsAsync(userId, stawka);

            RouletteStakes.Remove(userId);

            await msg.ModifyAsync(m => m.Content =
                $"🎯 Wypadło **{finalColor} ({finalNumber})**!\n" +
                (win ? $"🎉 Wygrałeś **{reward}**!" : $"💀 Przegrałeś **{stawka}**"));
        }

        // -----------------------------
        //            BET
        // -----------------------------

        [SlashCommand("bet", "50/50 — podwój stawkę.")]
        public async Task Bet(int amount)
        {
            if (amount <= 0)
            {
                await RespondAsync("Kwota musi być > 0", ephemeral: true);
                return;
            }

            var user = await UserDataManager.GetUserAsync(Context.User.Id);
            if (user.Credits < amount)
            {
                await RespondAsync("Nie masz tyle kredytów!", ephemeral: true);
                return;
            }

            var rand = new Random();
            bool win = rand.NextDouble() < 0.5;

            if (win)
                await UserDataManager.AddCreditsAsync(Context.User.Id, amount);
            else
                await UserDataManager.RemoveCreditsAsync(Context.User.Id, amount);

            var newData = await UserDataManager.GetUserAsync(Context.User.Id);

            await RespondAsync(
                win ?
                $"🎉 Wygrałeś! Zdobywasz {amount}. Nowy balans: {newData.Credits}" :
                $"💀 Przegrałeś {amount}. Nowy balans: {newData.Credits}"
            );
        }

        // -----------------------------
        //           LEADERBOARD
        // -----------------------------

        [SlashCommand("leaderboard", "Top 10 najbogatszych.")]
        public async Task Leaderboard()
        {
            await DeferAsync();

            var list = await UserDataManager.GetTopUsersLeaderboardAsync(10);
            if (list.Count == 0)
            {
                await FollowupAsync("Brak danych.");
                return;
            }

            string lines = string.Join("\n",
                list.Select((x, i) => $"**#{i + 1}** <@{x.UserId}> — {x.Credits} kredytów"));

            await FollowupAsync(lines);
        }

        // -----------------------------
        //           DAILY
        // -----------------------------

        [SlashCommand("dzienne", "Odbierz nagrodę dzienną.")]
        public async Task Daily()
        {
            await DeferAsync();

            ulong uid = Context.User.Id;

            if (!await UserDataManager.CanClaimDailyAsync(uid))
            {
                var remain = await UserDataManager.GetDailyCooldownRemainingAsync(uid);
                await FollowupAsync($"Odbierzesz za {remain.Hours}h {remain.Minutes}m");
                return;
            }

            int reward = new Random().Next(100, 251);

            await UserDataManager.AddCreditsAsync(uid, reward);
            await UserDataManager.SetDailyClaimAsync(uid);

            var data = await UserDataManager.GetUserAsync(uid);

            await FollowupAsync($"🎁 Otrzymujesz {reward}! Nowy balans: {data.Credits}");
        }

        // -----------------------------
        //       GRANT CREDITS ADMIN
        // -----------------------------

        [SlashCommand("grantcredits", "ADMIN: dodaj kredyty.")]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        public async Task GrantCredits(IUser target, int amount)
        {
            if (amount <= 0)
            {
                await RespondAsync("Kwota > 0!", ephemeral: true);
                return;
            }

            await UserDataManager.AddCreditsAsync(target.Id, amount);
            var newData = await UserDataManager.GetUserAsync(target.Id);

            await RespondAsync($"Dodano {amount}. Nowy balans: {newData.Credits}");
        }
    }
}
