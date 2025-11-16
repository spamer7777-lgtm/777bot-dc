using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Commands
{
    public class NoGroup : InteractionModuleBase<SocketInteractionContext>
    {
        private static readonly HttpClient http = new();

        [SlashCommand("ping", "Zobacz ping bota.")]
        public async Task Ping() => await RespondAsync($"🏓 Pong! Opóźnienie klienta: **{Bot.Client.Latency}** ms.");

        [SlashCommand("hi", "Powiedz Siemano!")]
        public async Task Hi([Summary("user", "Użytkownik, do którego chcesz powiedzieć siemano.")] IUser user) => await RespondAsync($"👋 HEEEJ! {user.Mention}!");

        [SlashCommand("balance", "Sprawdź swój aktualny balans kredytów.")]
        public async Task Balance()
        {
            var user = await UserDataManager.GetUserAsync(Context.User.Id);
            var embed = new EmbedBuilder()
                .WithTitle($"💰 Balans użytkownika: {Context.User.Username}")
                .WithDescription($"Masz **{user.Credits}** kredytów.")
                .WithColor(Color.Gold)
                .Build();

            await RespondAsync(embed: embed);
        }

        [SlashCommand("slots", "Sprawdź swoje szczęście")]
        public async Task Slots([Summary("amount", "Kwota, którą chcesz postawić")] int amount = 10)
        {
            if (amount <= 0) { await RespondAsync("⚠️ Podaj kwotę większą niż 0.", ephemeral: true); return; }

            var user = await UserDataManager.GetUserAsync(Context.User.Id);
            if (user.Credits < amount) { await RespondAsync($"🚫 Nie masz wystarczająco kredytów! Masz tylko {user.Credits}.", ephemeral: true); return; }

            await DeferAsync();
            await UserDataManager.RemoveCreditsAsync(Context.User.Id, amount);

            string[] icons = { "🍒", "🍋", "🍉", "💎", "7️⃣" };
            string[] effects = { "🔔", "✨", "💥", "🎵", "⭐", "⚡" };
            var rand = new Random();

            var embed = new EmbedBuilder()
                .WithTitle("🎰 777 Slots 🎰")
                .WithDescription("[⬜][⬜][⬜] Kręcimy...")
                .WithColor(Color.DarkGrey)
                .WithFooter($"Twój nowy balans: {user.Credits} kredytów")
                .Build();

            var msg = await FollowupAsync(embed: embed) as IUserMessage;
            if (msg == null) return;

            for (int i = 0; i < 6; i++)
            {
                var spin = Enumerable.Range(0, 3).Select(_ => icons[rand.Next(icons.Length)]).ToArray();
                var effect1 = effects[rand.Next(effects.Length)];
                var effect2 = effects[rand.Next(effects.Length)];

                embed = new EmbedBuilder()
                    .WithTitle($"{effect2} 🎰 777 Slots 🎰 {effect1}")
                    .WithDescription($"[{spin[0]}][{spin[1]}][{spin[2]}] Kręcimy...")
                    .WithColor(Color.DarkGrey)
                    .WithFooter($"Twój nowy balans: {(await UserDataManager.GetUserAsync(Context.User.Id)).Credits} kredytów")
                    .Build();

                await msg.ModifyAsync(m => m.Embed = embed);
                await Task.Delay(250);
            }

            var finalResult = Enumerable.Range(0, 3).Select(_ => icons[rand.Next(icons.Length)]).ToArray();
            bool win = finalResult.Distinct().Count() == 1;
            int reward = amount * 5;
            if (win) await UserDataManager.AddCreditsAsync(Context.User.Id, reward);

            embed = new EmbedBuilder()
                .WithTitle("🎰 777 Slots 🎰")
                .WithDescription($"[{finalResult[0]}][{finalResult[1]}][{finalResult[2]}]\n" +
                                 (win ? $"💰 **JACKPOT! WYGRAŁEŚ/AŚ {reward} kredytów!**" : $"😢 Przegrałeś/aś {amount} kredytów. Następnym razem lepiej!"))
                .WithColor(win ? Color.Gold : Color.DarkGrey)
                .WithFooter($"Twój nowy balans: {(await UserDataManager.GetUserAsync(Context.User.Id)).Credits} kredytów")
                .Build();

            await msg.ModifyAsync(m => m.Embed = embed);
        }

        [SlashCommand("ruletka", "Postaw zakład na kolor w ruletce!")]
        public async Task Ruletka([Summary("stawka", "Kwota, którą chcesz postawić.")] int stawka)
        {
            if (stawka <= 0) { await RespondAsync("⚠️ Podaj kwotę większą niż 0.", ephemeral: true); return; }

            var user = await UserDataManager.GetUserAsync(Context.User.Id);
            if (user.Credits < stawka) { await RespondAsync($"🚫 Nie masz wystarczająco kredytów! Masz tylko {user.Credits}.", ephemeral: true); return; }

            var builder = new ComponentBuilder()
                .WithButton("🔴 Czerwony", "roulette_red", ButtonStyle.Danger)
                .WithButton("⚫ Czarny", "roulette_black", ButtonStyle.Secondary)
                .WithButton("🟩 Zielony (0)", "roulette_green", ButtonStyle.Success);

            var embed = new EmbedBuilder()
                .WithTitle("🎡 Ruletka kasynowa 🎡")
                .WithDescription($"Wybierz kolor, na który chcesz postawić!\nStawka: **{stawka}** kredytów.")
                .WithColor(Color.DarkTeal)
                .Build();

            await RespondAsync(embed: embed, components: builder.Build());

            Bot.Client.ButtonExecuted += async component =>
            {
                if (component.User.Id != Context.User.Id) return;
                if (!component.Data.CustomId.StartsWith("roulette_")) return;

                await component.DeferAsync();
                string colorChoice = component.Data.CustomId.Split('_')[1];
                await UserDataManager.RemoveCreditsAsync(Context.User.Id, stawka);

                var rand = new Random();
                int finalNumber = rand.Next(0, 37);
                string outcomeColor = finalNumber == 0 ? "green" : (finalNumber % 2 == 0 ? "black" : "red");

                var msg = await component.FollowupAsync(embed: new EmbedBuilder().WithTitle("🎡 Ruletka się kręci!").WithDescription("Kula wiruje... 🎲").WithColor(Color.DarkGrey).Build()) as IUserMessage;

                List<int> spinSequence = Enumerable.Range(0, 15).Select(_ => rand.Next(0, 37)).Append(finalNumber).ToList();
                for (int i = 0; i < spinSequence.Count; i++)
                {
                    int num = spinSequence[i];
                    string col = num == 0 ? "🟩" : (num % 2 == 0 ? "⚫" : "🔴");
                    var spinStep = new EmbedBuilder().WithTitle("🎡 Ruletka się kręci!").WithDescription($"Kula toczy się... **{col} {num}**").WithColor(Color.DarkGrey).Build();
                    await msg.ModifyAsync(m => m.Embed = spinStep);
                    await Task.Delay(150 + (i * 80));
                }

                bool win = colorChoice == outcomeColor;
                int reward = colorChoice == "green" ? stawka * 14 : stawka * 2;
                if (win) await UserDataManager.AddCreditsAsync(Context.User.Id, reward);

                var result = new EmbedBuilder()
                    .WithTitle("🎯 Wynik ruletki!")
                    .WithDescription($"Wypadło **{finalNumber}** ({(outcomeColor switch { "red" => "🔴 Czerwony", "black" => "⚫ Czarny", _ => "🟩 Zielony" })})!\n\n" +
                                     (win ? $"🎉 Wygrałeś/aś **{reward}** kredytów!" : $"💀 Przegrałeś/aś **{stawka}** kredytów."))
                    .WithColor(win ? Color.Gold : Color.DarkRed)
                    .WithFooter($"Nowy balans: {(await UserDataManager.GetUserAsync(Context.User.Id)).Credits} kredytów")
                    .Build();

                await msg.ModifyAsync(m => m.Embed = result);
            };
        }

        [SlashCommand("bet", "Postaw zakład i spróbuj podwoić swoje kredyty!")]
        public async Task Bet([Summary("amount", "Kwota, którą chcesz postawić.")] int amount)
        {
            if (amount <= 0) { await RespondAsync("⚠️ Podaj kwotę większą niż 0.", ephemeral: true); return; }

            var user = await UserDataManager.GetUserAsync(Context.User.Id);
            if (user.Credits < amount) { await RespondAsync($"🚫 Nie masz wystarczająco kredytów! Masz tylko {user.Credits}.", ephemeral: true); return; }

            var rand = new Random();
            bool win = rand.NextDouble() < 0.5;
            int newBalance;

            if (win) { await UserDataManager.AddCreditsAsync(Context.User.Id, amount); newBalance = (await UserDataManager.GetUserAsync(Context.User.Id)).Credits; }
            else { await UserDataManager.RemoveCreditsAsync(Context.User.Id, amount); newBalance = (await UserDataManager.GetUserAsync(Context.User.Id)).Credits; }

            var embed = new EmbedBuilder()
                .WithTitle(win ? "💰 🎉 WYGRAŁEŚ!" : "💀 😢 PRZEGRAŁEŚ!")
                .WithDescription(win ? $"Twoje **{amount}** kredytów zostało podwojone! 💸\n💳 Nowy balans: **{newBalance}**" : $"Straciłeś/aś **{amount}** kredytów. 😔\n💳 Aktualny balans: **{newBalance}**")
                .WithColor(win ? Color.Gold : Color.DarkRed)
                .WithThumbnailUrl("https://i.imgur.com/DKOV6ZU.png")
                .WithFooter($"Zakręcił: {Context.User.Username}", Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed);
        }

        [SlashCommand("leaderboard", "Zobacz top 10 najbogatszych graczy!")]
        public async Task Leaderboard()
        {
            await DeferAsync();
            List<(ulong UserId, int Credits)> topUsers;
            try { topUsers = await UserDataManager.GetTopUsersLeaderboardAsync(10); }
            catch (Exception ex) { await FollowupAsync($"❌ Błąd podczas pobierania danych: {ex.Message}"); return; }

            if (!topUsers.Any()) { await FollowupAsync("📉 Brak danych o użytkownikach."); return; }
            var desc = string.Join("\n", topUsers.Select((u, i) => $"**#{i + 1}** <@{u.UserId}> — 💰 {u.Credits} kredytów"));

            var embed = new EmbedBuilder().WithTitle("🏆 Tablica Najbogatszych 🏆").WithDescription(desc).WithColor(Color.Gold).WithFooter("Czy uda ci się wejść do TOP 10?").Build();
            await FollowupAsync(embed: embed);
        }

        [SlashCommand("dzienne", "Odbierz swoje dzienne kredyty!")]
        public async Task Daily()
        {
            await DeferAsync();
            var userId = Context.User.Id;

            if (!await UserDataManager.CanClaimDailyAsync(userId))
            {
                var remaining = await UserDataManager.GetDailyCooldownRemainingAsync(userId);
                var embedCooldown = new EmbedBuilder()
                    .WithTitle("⏰ Już odebrałeś/aś dzienną nagrodę!")
                    .WithDescription($"Spróbuj ponownie za **{remaining.Hours}h {remaining.Minutes}m**.")
                    .WithColor(Color.Orange)
                    .WithFooter("Odbierz swoją nagrodę jutro 🎁")
                    .Build();

                await FollowupAsync(embed: embedCooldown, ephemeral: true);
                return;
            }

            int reward = new Random().Next(100, 251);
            await UserDataManager.AddCreditsAsync(userId, reward);
            await UserDataManager.SetDailyClaimAsync(userId);

            var newBalance = (await UserDataManager.GetUserAsync(userId)).Credits;
            var embed = new EmbedBuilder()
                .WithTitle("🎁 Dzienna nagroda!")
                .WithDescription($"Odebrałeś/aś **{reward}** kredytów.\n💰 Nowy balans: **{newBalance}**")
                .WithColor(Color.Gold)
                .WithFooter("Dziękujemy za grę — wróć jutro po kolejne nagrody!")
                .Build();

            await FollowupAsync(embed: embed);
        }
        

        [SlashCommand("grantcredits", "Administrator: dodaj kredyty użytkownikowi (ukryta).")]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        public async Task GrantCredits([Summary("user", "Użytkownik, któremu chcesz dodać kredyty.")] IUser target, [Summary("amount", "Liczba kredytów do dodania.")] int amount)
        {
            ulong ownerId = 299929951451217921;
            if (Context.User.Id != ownerId && !((SocketGuildUser)Context.User).GuildPermissions.Administrator)
            { await RespondAsync("🚫 Nie masz uprawnień do użycia tej komendy.", ephemeral: true); return; }

            if (amount <= 0) { await RespondAsync("⚠️ Ilość musi być większa niż 0.", ephemeral: true); return; }

            await UserDataManager.AddCreditsAsync(target.Id, amount);
            var newBalance = (await UserDataManager.GetUserAsync(target.Id)).Credits;

            await RespondAsync($"✅ Dodano **{amount}** kredytów użytkownikowi {target.Mention}. Nowy balans: **{newBalance}**", ephemeral: true);
        }
    }
}


