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
        public async Task Ping()
        {
            await RespondAsync($"🏓 Pong! Opóźnienie klienta: **{Bot.Client.Latency}** ms.");
        }

        [SlashCommand("hi", "Powiedz Siemano!")]
        public async Task Hi([Summary("user", "Użytkownik, do którego chcesz powiedzieć siemano.")] IUser user)
        {
            await RespondAsync($"👋 HEEEJ! {user.Mention}!");
        }

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
public async Task Slots(
    [Summary("amount", "Kwota, którą chcesz postawić")] int amount = 10)
{
    if (amount <= 0)
    {
        await RespondAsync("⚠️ Podaj kwotę większą niż 0.", ephemeral: true);
        return;
    }

    var user = await UserDataManager.GetUserAsync(Context.User.Id);
    if (user.Credits < amount)
    {
        await RespondAsync($"🚫 Nie masz wystarczająco kredytów! Masz tylko {user.Credits}.", ephemeral: true);
        return;
    }

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
    int reward = amount * 5; // Reward scales with the bet amount
    if (win) await UserDataManager.AddCreditsAsync(Context.User.Id, reward);

    embed = new EmbedBuilder()
        .WithTitle("🎰 777 Slots 🎰")
        .WithDescription($"[{finalResult[0]}][{finalResult[1]}][{finalResult[2]}]\n" +
                         (win ? $"💰 **JACKPOT! WYGRAŁEŚ/AŚ {reward} kredytów!**" :
                                $"😢 Przegrałeś/aś {amount} kredytów. Następnym razem lepiej!"))
        .WithColor(win ? Color.Gold : Color.DarkGrey)
        .WithFooter($"Twój nowy balans: {(await UserDataManager.GetUserAsync(Context.User.Id)).Credits} kredytów")
        .Build();

    await msg.ModifyAsync(m => m.Embed = embed);
}


        [SlashCommand("bet", "Postaw zakład i spróbuj podwoić swoje kredyty!")]
        public async Task Bet([Summary("amount", "Kwota, którą chcesz postawić.")] int amount)
        {
            if (amount <= 0)
            {
                await RespondAsync("⚠️ Podaj kwotę większą niż 0.", ephemeral: true);
                return;
            }

            var user = await UserDataManager.GetUserAsync(Context.User.Id);
            if (user.Credits < amount)
            {
                await RespondAsync($"🚫 Nie masz wystarczająco kredytów! Masz tylko {user.Credits}.", ephemeral: true);
                return;
            }

            var rand = new Random();
            bool win = rand.NextDouble() < 0.5;

            string resultEmoji = win ? "💰" : "💀";
            string title = win ? "🎉 WYGRAŁEŚ!" : "😢 PRZEGRAŁEŚ!";
            string description;
            Color color;

            if (win)
            {
                await UserDataManager.AddCreditsAsync(Context.User.Id, amount);
                var newBalance = (await UserDataManager.GetUserAsync(Context.User.Id)).Credits;
                description = $"Twoje **{amount}** kredytów zostało podwojone! 💸\n💳 Nowy balans: **{newBalance}**";
                color = Color.Gold;
            }
            else
            {
                await UserDataManager.RemoveCreditsAsync(Context.User.Id, amount);
                var newBalance = (await UserDataManager.GetUserAsync(Context.User.Id)).Credits;
                description = $"Straciłeś/aś **{amount}** kredytów. 😔\n💳 Aktualny balans: **{newBalance}**";
                color = Color.DarkRed;
            }

            var embed = new EmbedBuilder()
                .WithTitle($"{resultEmoji} {title}")
                .WithDescription(description)
                .WithColor(color)
                .WithThumbnailUrl("https://e7.pngegg.com/pngimages/542/1006/png-clipart-poker-chips-illustration-blackjack-online-casino-online-poker-roulette-bargaining-chip-game-electronics-thumbnail.png")
                .WithFooter($"Zakręcił: {Context.User.Username}", Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed);
        }

[SlashCommand("leaderboard", "Zobacz top 10 najbogatszych graczy!")]
public async Task Leaderboard()
{
    await DeferAsync(); // Tell Discord we are processing

    List<(ulong UserId, int Credits)> topUsers;
    try
    {
        topUsers = await UserDataManager.GetTopUsersLeaderboardAsync(10);
    }
    catch (Exception ex)
    {
        await FollowupAsync($"❌ Błąd podczas pobierania danych: {ex.Message}");
        return;
    }

    if (!topUsers.Any())
    {
        await FollowupAsync("📉 Brak danych o użytkownikach.");
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

            var rand = new Random();
            int reward = rand.Next(100, 251);
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
[SlashCommand("roulette_full", "Zagraj w pełną ruletkę (0-36)!")]
        public async Task RouletteFull([Summary("bet", "Kwota, którą chcesz postawić")] int bet)
        {
            if (bet <= 0)
            {
                await RespondAsync("⚠️ Podaj kwotę większą niż 0.", ephemeral: true);
                return;
            }

            var user = await UserDataManager.GetUserAsync(Context.User.Id);
            if (user.Credits < bet)
            {
                await RespondAsync($"🚫 Nie masz wystarczająco kredytów! Masz tylko {user.Credits}.", ephemeral: true);
                return;
            }

            await UserDataManager.RemoveCreditsAsync(Context.User.Id, bet);

            // Build the 0-36 roulette number buttons
            var builder = new ComponentBuilder();
            int rowCount = 0;
            for (int i = 0; i <= 36; i++)
            {
                string color = i == 0 ? "🟢" : (i % 2 == 0 ? "⚫" : "🔴");
                string label = $"{color} {i}";
                builder.WithButton(label, $"roulette_number_{i}_{Context.User.Id}_{bet}", ButtonStyle.Secondary);

                rowCount++;
                if (rowCount % 5 == 0 && i != 36) builder.WithRow(); // Discord allows max 5 buttons per row
            }

            var embed = new EmbedBuilder()
                .WithTitle("🎡 Pełna ruletka 0-36 🎡")
                .WithDescription($"Twój zakład: **{bet} kredytów**\nKliknij numer, na który chcesz postawić!")
                .WithColor(Color.Purple)
                .Build();

            await RespondAsync(embed: embed, components: builder.Build());
        }

        [ComponentInteraction("roulette_number_*")]
        public async Task RouletteFullClick(string customId)
        {
            await DeferAsync();

            // Format: roulette_number_<number>_<userId>_<bet>
            var parts = customId.Split('_');
            if (parts.Length != 5) return;

            int chosenNumber = int.Parse(parts[2]);
            ulong userId = ulong.Parse(parts[3]);
            int bet = int.Parse(parts[4]);

            if (Context.User.Id != userId)
            {
                await FollowupAsync("🚫 To nie Twój zakład!", ephemeral: true);
                return;
            }

            var user = await UserDataManager.GetUserAsync(userId);

            // Spin roulette
            int winningNumber = rand.Next(0, 37);
            string winningColor = winningNumber == 0 ? "green" : (winningNumber % 2 == 0 ? "black" : "red");
            string chosenColor = chosenNumber == 0 ? "green" : (chosenNumber % 2 == 0 ? "black" : "red");

            int reward = 0;
            bool win = false;

            if (chosenNumber == winningNumber)
            {
                reward = bet * 35; // Standard roulette payout
                win = true;
            }

            if (win) await UserDataManager.AddCreditsAsync(userId, reward);

            int newBalance = (await UserDataManager.GetUserAsync(userId)).Credits;

            var embed = new EmbedBuilder()
                .WithTitle(win ? "🎉 WYGRAŁEŚ W PEŁNEJ ROULETTE!" : "😢 Przegrałeś w ruletkę!")
                .WithDescription($"🎯 Wylosowana liczba: **{winningNumber}** ({winningColor})\n" +
                                 (win ? $"💰 Wygrałeś **{reward} kredytów!**" : $"💸 Straciłeś **{bet} kredytów.**") +
                                 $"\n💳 Nowy balans: **{newBalance}**")
                .WithColor(win ? Color.Gold : Color.DarkRed)
                .Build();

            await FollowupAsync(embed: embed);
        }
    
        [SlashCommand("grantcredits", "Administrator: dodaj kredyty użytkownikowi (ukryta).")]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        public async Task GrantCredits(
            [Summary("user", "Użytkownik, któremu chcesz dodać kredyty.")] IUser target,
            [Summary("amount", "Liczba kredytów do dodania.")] int amount)
        {
            ulong ownerId = 299929951451217921;

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

            await UserDataManager.AddCreditsAsync(target.Id, amount);
            var newBalance = (await UserDataManager.GetUserAsync(target.Id)).Credits;

            await RespondAsync(
                $"✅ Dodano **{amount}** kredytów użytkownikowi {target.Mention}. Nowy balans: **{newBalance}**",
                ephemeral: true
            );
        }
    }
}





