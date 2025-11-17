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
        public static Dictionary<ulong, int> RouletteStakes = new();

        // =========================================================
        //                       HELPERS
        // =========================================================
        private Embed Error(string msg) =>
            new EmbedBuilder()
                .WithTitle("❌ Błąd")
                .WithDescription(msg)
                .WithColor(Color.Red)
                .Build();

        private Embed Loading(string msg) =>
            new EmbedBuilder()
                .WithDescription(msg)
                .WithColor(Color.DarkGrey)
                .Build();

        // =========================================================
        //                          PING
        // =========================================================
        [SlashCommand("ping", "Sprawdź opóźnienie.")]
        public async Task Ping()
        {
            var embed = new EmbedBuilder()
                .WithTitle("🏓 Pong!")
                .WithDescription($"Opóźnienie: **{Bot.Client.Latency} ms**")
                .WithColor(Color.Green)
                .Build();

            await RespondAsync(embed: embed);
        }

        // =========================================================
        //                           HI
        // =========================================================
        [SlashCommand("hi", "Powiedz siemano.")]
        public async Task Hi(IUser user)
        {
            var embed = new EmbedBuilder()
                .WithTitle("👋 Siemano!")
                .WithDescription($"{user.Mention}, witam cię serdecznie!")
                .WithColor(Color.Gold)
                .Build();

            await RespondAsync(embed: embed);
        }

        // =========================================================
        //                        BALANCE
        // =========================================================
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

        // =========================================================
        //                         SLOTS
        // =========================================================
        [SlashCommand("slots", "Zagraj w jednorękiego bandytę.")]
        public async Task Slots(int amount = 10)
        {
            if (amount <= 0)
                { await RespondAsync(embed: Error("Kwota musi być większa niż 0!"), ephemeral: true); return; }

            var data = await UserDataManager.GetUserAsync(Context.User.Id);

            if (data.Credits < amount)
                { await RespondAsync(embed: Error($"Masz tylko **{data.Credits}** kredytów!"), ephemeral: true); return; }

            await DeferAsync();

            await UserDataManager.RemoveCreditsAsync(Context.User.Id, amount);

            string[] icons = { "🍒", "🍋", "🍉", "💎", "7️⃣" };
            var rand = new Random();

            var msg = await FollowupAsync(embed: Loading("🎰 Kręcimy...")) as IUserMessage;

            for (int i = 0; i < 5; i++)
            {
                string roll = string.Join(" ", Enumerable.Range(0, 3).Select(_ => icons[rand.Next(icons.Length)]));
                await msg.ModifyAsync(m => m.Embed = Loading($"🎰 {roll}\nKręcimy..."));
                await Task.Delay(220);
            }

            string[] final = Enumerable.Range(0, 3).Select(_ => icons[rand.Next(icons.Length)]).ToArray();
            bool win = final[0] == final[1] && final[1] == final[2];
            int reward = win ? amount * 5 : 0;

            if (win) await UserDataManager.AddCreditsAsync(Context.User.Id, reward);

            var embed = new EmbedBuilder()
                .WithTitle("🎰 Wynik Slots")
                .WithDescription($"**{final[0]} {final[1]} {final[2]}**\n\n" +
                                 (win
                                 ? $"🎉 WYGRAŁEŚ **{reward}** kredytów!"
                                 : $"💀 Przegrałeś **{amount}** kredytów."))
                .WithColor(win ? Color.Gold : Color.DarkRed)
                .WithFooter($"Balans: {(await UserDataManager.GetUserAsync(Context.User.Id)).Credits}")
                .Build();

            await msg.ModifyAsync(m => m.Embed = embed);
        }


        // =========================================================
        //                     RULETKA COMMAND
        // =========================================================
        [SlashCommand("ruletka", "Postaw zakład.")]
        public async Task Ruletka(int stawka)
        {
            if (stawka <= 0) { await RespondAsync(embed: Error("Kwota musi być > 0!"), ephemeral: true); return; }

            var data = await UserDataManager.GetUserAsync(Context.User.Id);
            if (data.Credits < stawka) { await RespondAsync(embed: Error("Nie masz tyle kredytów!"), ephemeral: true); return; }

            RouletteStakes[Context.User.Id] = stawka;

            var embed = new EmbedBuilder()
                .WithTitle("🎡 Ruletka")
                .WithDescription($"Stawiasz **{stawka}** kredytów.\nWybierz kolor poniżej.")
                .WithColor(Color.Teal)
                .Build();

            var buttons = new ComponentBuilder()
                .WithButton("🔴 Czerwony", "roulette_red", ButtonStyle.Danger)
                .WithButton("⚫ Czarny", "roulette_black", ButtonStyle.Secondary)
                .WithButton("🟩 Zielony (0)", "roulette_green", ButtonStyle.Success);

            await RespondAsync(embed: embed, components: buttons.Build());
        }


        // =========================================================
        //              STATIC BUTTON HANDLER (SAFE)
        // =========================================================
        public static async Task HandleRouletteButtonsStatic(SocketMessageComponent component)
        {
            if (!component.Data.CustomId.StartsWith("roulette_"))
                return;

            try
            {
                await component.DeferAsync();

                ulong uid = component.User.Id;

                if (!RouletteStakes.TryGetValue(uid, out int stawka))
                {
                    await component.FollowupAsync(embed:
                        new EmbedBuilder()
                            .WithTitle("❌ Błąd")
                            .WithDescription("Nie masz aktywnej ruletki.")
                            .WithColor(Color.Red)
                            .Build(),
                        ephemeral: true);
                    return;
                }

                string choice = component.Data.CustomId.Replace("roulette_", "");

                var rand = new Random();
                int finalNum = rand.Next(0, 37);
                string finalColor = finalNum == 0 ? "green" :
                                    finalNum % 2 == 0 ? "black" : "red";

                var msg = await component.FollowupAsync(embed:
                    new EmbedBuilder()
                        .WithDescription("🎡 Kula się kręci...")
                        .WithColor(Color.DarkGrey)
                        .Build()) as IUserMessage;

                // Animation
                foreach (int n in Enumerable.Range(0, 12).Select(_ => rand.Next(0, 37)).Append(finalNum))
                {
                    string icon = n == 0 ? "🟩" : (n % 2 == 0 ? "⚫" : "🔴");

                    await msg.ModifyAsync(m => m.Embed =
                        new EmbedBuilder()
                            .WithDescription($"🎲 **{icon} {n}**")
                            .WithColor(Color.DarkGrey)
                            .Build());

                    await Task.Delay(130);
                }

                bool win = choice == finalColor;

                int reward =
                    finalColor == "green" ? stawka * 14 :
                    win ? stawka * 2 : 0;

                if (win)
                    await UserDataManager.AddCreditsAsync(uid, reward);
                else
                    await UserDataManager.RemoveCreditsAsync(uid, stawka);

                RouletteStakes.Remove(uid);

                var resultEmbed = new EmbedBuilder()
                    .WithTitle("🎯 Wynik Ruletki")
                    .WithDescription(
                        $"Wypadło **{finalNum}** ({finalColor})!\n\n" +
                        (win
                        ? $"🎉 WYGRAŁEŚ **{reward}** kredytów!"
                        : $"💀 Przegrałeś **{stawka}** kredytów.")
                    )
                    .WithColor(win ? Color.Green : Color.Red)
                    .WithFooter($"Balans: {(await UserDataManager.GetUserAsync(uid)).Credits}")
                    .Build();

                await msg.ModifyAsync(m => m.Embed = resultEmbed);
            }
            catch (Exception ex)
            {
                await component.FollowupAsync(embed:
                    new EmbedBuilder()
                        .WithTitle("❌ Błąd ruletki")
                        .WithDescription(ex.Message)
                        .WithColor(Color.Red)
                        .Build(),
                    ephemeral: true);
            }
        }


        // =========================================================
        //                          BET
        // =========================================================
        [SlashCommand("bet", "50/50 — podwój stawkę.")]
        public async Task Bet(int amount)
        {
            if (amount <= 0)
                { await RespondAsync(embed: Error("Kwota musi być > 0!"), ephemeral: true); return; }

            var data = await UserDataManager.GetUserAsync(Context.User.Id);

            if (data.Credits < amount)
                { await RespondAsync(embed: Error("Nie masz tyle kredytów!"), ephemeral: true); return; }

            var rand = new Random();
            bool win = rand.NextDouble() < 0.5;

            if (win)
                await UserDataManager.AddCreditsAsync(Context.User.Id, amount);
            else
                await UserDataManager.RemoveCreditsAsync(Context.User.Id, amount);

            data = await UserDataManager.GetUserAsync(Context.User.Id);

            var embed = new EmbedBuilder()
                .WithTitle(win ? "🎉 Wygrana!" : "💀 Przegrana!")
                .WithDescription(win
                    ? $"Podwajasz **{amount}** kredytów!"
                    : $"Straciłeś **{amount}** kredytów.")
                .WithColor(win ? Color.Gold : Color.DarkRed)
                .WithFooter($"Balans: {data.Credits}")
                .Build();

            await RespondAsync(embed: embed);
        }

        // =========================================================
        //                        LEADERBOARD
        // =========================================================
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

            string text = string.Join("\n",
                list.Select((x, i) => $"**#{i + 1}** <@{x.UserId}> — **{x.Credits}** kredytów"));

            var embed = new EmbedBuilder()
                .WithTitle("🏆 TOP 10")
                .WithDescription(text)
                .WithColor(Color.Gold)
                .Build();

            await FollowupAsync(embed: embed);
        }

        // =========================================================
        //                          DAILY
        // =========================================================
        [SlashCommand("dzienne", "Odbierz nagrodę dzienną.")]
        public async Task Daily()
        {
            await DeferAsync();

            ulong uid = Context.User.Id;

            if (!await UserDataManager.CanClaimDailyAsync(uid))
            {
                var remain = await UserDataManager.GetDailyCooldownRemainingAsync(uid);
                await FollowupAsync(embed: Error($"Spróbuj za **{remain.Hours}h {remain.Minutes}m**."));
                return;
            }

            int reward = new Random().Next(100, 251);
            await UserDataManager.AddCreditsAsync(uid, reward);
            await UserDataManager.SetDailyClaimAsync(uid);

            var data = await UserDataManager.GetUserAsync(uid);

            var embed = new EmbedBuilder()
                .WithTitle("🎁 Dzienna nagroda!")
                .WithDescription($"Otrzymujesz **{reward}** kredytów!\nBalans: **{data.Credits}**")
                .WithColor(Color.Green)
                .Build();

            await FollowupAsync(embed: embed);
        }

        // =========================================================
        //                         GRANT CREDITS (ADMIN)
        // =========================================================
        [SlashCommand("grantcredits", "Dodaj kredyty (admin).")]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        public async Task GrantCredits(IUser target, int amount)
        {
            if (amount <= 0)
            {
                await RespondAsync(embed: Error("Kwota musi być > 0!"), ephemeral: true);
                return;
            }

            await UserDataManager.AddCreditsAsync(target.Id, amount);
            var data = await UserDataManager.GetUserAsync(target.Id);

            var embed = new EmbedBuilder()
                .WithTitle("🛠 ADMIN")
                .WithDescription($"Dodano **{amount}** kredytów użytkownikowi {target.Mention}.")
                .WithColor(Color.Blue)
                .WithFooter($"Nowy balans: {data.Credits}")
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);
        }
    }
}
