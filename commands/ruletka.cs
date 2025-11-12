using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Commands
{
    public class InteractiveRoulette : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly Random _rand = new Random();

        private readonly (string ColorName, int Number)[] RouletteNumbers =
        {
            ("🟥", 1), ("⬛", 2), ("🟥", 3), ("⬛", 4), ("🟥", 5), ("⬛", 6),
            ("🟥", 7), ("⬛", 8), ("🟥", 9), ("⬛", 10), ("🟥", 11), ("⬛", 12),
            ("🟥", 13), ("⬛", 14), ("🟥", 15), ("⬛", 16), ("🟥", 17), ("⬛", 18),
            ("🟥", 19), ("⬛", 20), ("🟥", 21), ("⬛", 22), ("🟥", 23), ("⬛", 24),
            ("🟥", 25), ("⬛", 26), ("🟥", 27), ("⬛", 28), ("🟥", 29), ("⬛", 30),
            ("🟥", 31), ("⬛", 32), ("🟥", 33), ("⬛", 34), ("🟥", 35), ("⬛", 36),
            ("🟩", 0)
        };

        [SlashCommand("ruletka", "Zagraj w interaktywną ruletkę!")]
        public async Task Roulette([Summary("amount", "Kwota zakładu")] int amount)
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

            // Tworzymy przyciski do obstawiania
            var builder = new ComponentBuilder()
                .WithButton("🔴 Czerwone", "roulette_red", ButtonStyle.Danger)
                .WithButton("⚫ Czarne", "roulette_black", ButtonStyle.Secondary)
                .WithButton("🟢 Zielone (0)", "roulette_green", ButtonStyle.Success);

            // Embed informacyjny
            var embed = new EmbedBuilder()
                .WithTitle("🎡 Interaktywna Ruletka 🎡")
                .WithDescription($"Obstaw zakład: **{amount} kredytów**\nKliknij przycisk, aby wybrać kolor.")
                .WithColor(Color.DarkBlue)
                .Build();

            var msg = await FollowupAsync(embed: embed, components: builder.Build()) as IUserMessage;

            // Event handler dla przycisków
            async Task ComponentHandler(SocketMessageComponent comp)
            {
                if (comp.User.Id != Context.User.Id) 
                {
                    await comp.RespondAsync("⛔ To nie Twój zakład!", ephemeral: true);
                    return;
                }

                string userBet = comp.Data.CustomId switch
                {
                    "roulette_red" => "red",
                    "roulette_black" => "black",
                    "roulette_green" => "green",
                    _ => null
                };

                if (userBet == null) return;

                // Obrót ruletki
                var spinResult = RouletteNumbers[_rand.Next(RouletteNumbers.Length)];
                bool win = false;
                int reward = 0;

                if (userBet == "red" && spinResult.ColorName == "🟥") win = true;
                else if (userBet == "black" && spinResult.ColorName == "⬛") win = true;
                else if (userBet == "green" && spinResult.ColorName == "🟩") win = true;

                if (win)
                {
                    reward = userBet switch
                    {
                        "red" or "black" => amount * 2,
                        "green" => amount * 14,
                        _ => amount
                    };
                    await UserDataManager.AddCreditsAsync(Context.User.Id, reward);
                }

                int newBalance = (await UserDataManager.GetUserAsync(Context.User.Id)).Credits;

                var resultEmbed = new EmbedBuilder()
                    .WithTitle("🎡 Ruletka 🎡")
                    .WithDescription($"Twój zakład: **{amount} kredytów** na **{userBet}**\n" +
                                     $"Wynik: {spinResult.ColorName} {spinResult.Number}\n" +
                                     (win ? $"💰 WYGRAŁEŚ {reward} kredytów!" : $"😢 Przegrałeś {amount} kredytów."))
                    .WithColor(win ? Color.Gold : Color.DarkRed)
                    .WithFooter($"Twój nowy balans: {newBalance} kredytów")
                    .Build();

                // Wyłączamy przyciski po kliknięciu
                var disabledBuilder = new ComponentBuilder()
                    .WithButton("🔴 Czerwone", "roulette_red", ButtonStyle.Danger, disabled: true)
                    .WithButton("⚫ Czarne", "roulette_black", ButtonStyle.Secondary, disabled: true)
                    .WithButton("🟢 Zielone (0)", "roulette_green", ButtonStyle.Success, disabled: true);

                await comp.UpdateAsync(x =>
                {
                    x.Embed = resultEmbed;
                    x.Components = disabledBuilder.Build();
                });

                // Odsubskrybowanie eventu
                Context.Client.ButtonExecuted -= ComponentHandler;
            }

            Context.Client.ButtonExecuted += ComponentHandler;
        }
    }
}
