using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

// ✅ WYCENA: nowe usingi
using _777bot;

namespace Commands
{
    public class NoGroup : InteractionModuleBase<SocketInteractionContext>
    {
        public static Dictionary<ulong, int> RouletteStakes = new();

        private Embed Error(string msg) =>
            new EmbedBuilder()
                .WithTitle("❌ Błąd")
                .WithDescription(msg)
                .WithColor(Color.DarkRed)
                .WithCurrentTimestamp()
                .WithFooter($"Wywołano przez {Context.User.Username}", Context.User.GetAvatarUrl())
                .Build();

        private Embed Loading(string msg) =>
            new EmbedBuilder()
                .WithDescription(msg)
                .WithColor(Color.DarkGrey)
                .WithCurrentTimestamp()
                .Build();

        // =========================================================
        // PING
        // =========================================================
        [SlashCommand("ping", "Sprawdź opóźnienie.")]
        public async Task Ping()
        {
            await RespondAsync(embed:
                new EmbedBuilder()
                    .WithTitle("🏓 Pong!")
                    .WithDescription("Bot odpowiada poprawnie.")
                    .AddField("📡 Opóźnienie", $"**{Bot.Client.Latency} ms**", true)
                    .WithColor(Color.Gold)
                    .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl())
                    .WithFooter($"Wywołano przez {Context.User.Username}", Context.User.GetAvatarUrl())
                    .WithCurrentTimestamp()
                    .Build()
            );
        }

        // =========================================================
        // HI
        // =========================================================
        [SlashCommand("hi", "Powiedz siemano.")]
        public async Task Hi(IUser user)
        {
            await RespondAsync(embed:
                new EmbedBuilder()
                    .WithTitle("👋 Siemano!")
                    .WithDescription($"{user.Mention}, witam Cię serdecznie na serwerze!")
                    .AddField("Inicjator", Context.User.Mention, true)
                    .WithColor(Color.Gold)
                    .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                    .WithFooter($"Wywołano przez {Context.User.Username}", Context.User.GetAvatarUrl())
                    .WithCurrentTimestamp()
                    .Build()
            );
        }

        // =========================================================
        // BALANCE
        // =========================================================
        [SlashCommand("balance", "Sprawdź swój balans.")]
        public async Task Balance()
        {
            var data = await UserDataManager.GetUserAsync(Context.User.Id);

            await RespondAsync(embed:
                new EmbedBuilder()
                    .WithTitle("💰 Twój balans")
                    .WithDescription("Stan Twoich kredytów w kasynie:")
                    .AddField("Aktualny balans", $"**{data.Credits}** 💳", false)
                    .WithColor(Color.Gold)
                    .WithThumbnailUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                    .WithFooter($"Wywołano przez {Context.User.Username}", Context.User.GetAvatarUrl())
                    .WithCurrentTimestamp()
                    .Build()
            );
        }

        // =========================================================
        // ✅ WYCENA
        // =========================================================
        [SlashCommand("wycena", "Wycena pojazdu po VUID (jeśli brak w bazie, bot poprosi o wklejkę karty).")]
        public async Task Wycena([Summary("vuid", "ID pojazdu (VUID)")] int vuid)
        {
            if (Bot.VehicleStore == null || Bot.ValuationService == null)
            {
                await RespondAsync(embed: Error("Wycena nie jest zainicjalizowana (sprawdź logi oraz env: MONGO_URL / MONGO_DB)."), ephemeral: true);
                return;
            }

            // jeśli jest w bazie -> licz
            var existing = await Bot.VehicleStore.GetVehicleAsync(vuid);
            if (existing != null)
            {
                // jeśli są limitowane/unikatowe kolory bez ceny -> ustaw pending na ceny
                var missingSpecial = await GetMissingSpecialColorsAsync(existing);

                if (missingSpecial.Count > 0)
                {
                    Bot.PendingWycena[Context.User.Id] = new PendingWycenaState
                    {
                        Kind = PendingKind.WaitingForSpecialColorPrices,
                        Vuid = vuid,
                        UserId = Context.User.Id,
                        ChannelId = Context.Channel.Id,
                        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
                        MissingSpecialColors = missingSpecial
                    };

                    await RespondAsync(
                        "🧾 Brakuje cen dla limitowanych/unikatowych kolorów.\n" +
                        "Podaj je jako **zwykłą wiadomość** na tym kanale, np.:\n" +
                        "`licznik=35000`\n" +
                        "`swiatla=55000`\n" +
                        "(możesz podać 1–2 linie)",
                        ephemeral: true
                    );
                    return;
                }

                var result = await Bot.ValuationService.EvaluateAsync(existing);
                await RespondAsync(embed: result.BuildEmbed(vuid, existing));
                return;
            }

            // brak w bazie -> poproś o wklejkę
            Bot.PendingWycena[Context.User.Id] = new PendingWycenaState
            {
                Kind = PendingKind.WaitingForVehiclePaste,
                Vuid = vuid,
                UserId = Context.User.Id,
                ChannelId = Context.Channel.Id,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            };

            await RespondAsync(
                $"📋 Nie mam VUID **{vuid}** w bazie.\n" +
                $"Wklej teraz pełną kartę pojazdu (z **VUID / Model / Silnik / Tuning wizualny / Tuning mechaniczny / Kolor świateł / Kolor licznika**) jako zwykłą wiadomość na tym kanale.\n" +
                $"Masz **10 minut**.",
                ephemeral: true
            );
        }

        private static async Task<List<(SpecialColorType type, string name, string rarity)>> GetMissingSpecialColorsAsync(VehicleCard card)
        {
            var list = new List<(SpecialColorType type, string name, string rarity)>();

            // lights
            var (lName, lRarity) = VehicleCardParser.ParseColorWithRarity(card.LightsColorRaw);
            if (!string.IsNullOrWhiteSpace(lName) &&
                (lRarity.Equals("Limitowane", StringComparison.OrdinalIgnoreCase) || lRarity.Equals("Unikatowe", StringComparison.OrdinalIgnoreCase)))
            {
                var p = await Bot.VehicleStore.GetSpecialColorPriceAsync(SpecialColorType.Lights, lName, lRarity);
                if (!p.HasValue) list.Add((SpecialColorType.Lights, lName, lRarity));
            }

            // dashboard
            var (dName, dRarity) = VehicleCardParser.ParseColorWithRarity(card.DashboardColorRaw);
            if (!string.IsNullOrWhiteSpace(dName) &&
                (dRarity.Equals("Limitowane", StringComparison.OrdinalIgnoreCase) || dRarity.Equals("Unikatowe", StringComparison.OrdinalIgnoreCase)))
            {
                var p = await Bot.VehicleStore.GetSpecialColorPriceAsync(SpecialColorType.Dashboard, dName, dRarity);
                if (!p.HasValue) list.Add((SpecialColorType.Dashboard, dName, dRarity));
            }

            return list
                .GroupBy(x => (x.type, TextNorm.NormalizeKey(x.name), TextNorm.NormalizeKey(x.rarity)))
                .Select(g => g.First())
                .ToList();
        }

        // =========================================================
        // SLOTS
        // =========================================================
        [SlashCommand("slots", "Jednoręki bandyta.")]
        public async Task Slots(int amount = 10)
        {
            if (amount <= 0)
            {
                await RespondAsync(embed: Error("Kwota musi być większa niż 0."), ephemeral: true);
                return;
            }

            var data = await UserDataManager.GetUserAsync(Context.User.Id);

            if (data.Credits < amount)
            {
                await RespondAsync(embed: Error("Masz za mało kredytów."), ephemeral: true);
                return;
            }

            await DeferAsync();

            await UserDataManager.RemoveCreditsAsync(Context.User.Id, amount);

            string[] icons = { "🍒", "🍋", "🍉", "💎", "7️⃣" };
            var rand = new Random();

            var msg = await FollowupAsync(embed: Loading("🎰 Kręcimy bębny...")) as IUserMessage;

            // animation
            for (int i = 0; i < 5; i++)
            {
                string roll = string.Join(" ", Enumerable.Range(0, 3).Select(_ => icons[rand.Next(icons.Length)]));
                await msg.ModifyAsync(m => m.Embed = Loading($"🎰 {roll}\nKręcimy..."));
                await Task.Delay(200);
            }

            // final roll
            string[] final = Enumerable.Range(0, 3).Select(_ => icons[rand.Next(icons.Length)]).ToArray();
            bool win = final.All(x => x == final[0]);
            int reward = win ? amount * 5 : 0;

            if (win)
                await UserDataManager.AddCreditsAsync(Context.User.Id, reward);

            var finalData = await UserDataManager.GetUserAsync(Context.User.Id);

            var embed = new EmbedBuilder()
                .WithTitle("🎰 Wynik jednorękiego bandyty")
                .WithDescription($"**{final[0]} {final[1]} {final[2]}**")
                .AddField(win ? "🎉 Wygrana!" : "💀 Przegrana!",
                          win
                              ? $"Zgarnąłeś **{reward}** kredytów!"
                              : $"Straciłeś **{amount}** kredytów.",
                          false)
                .AddField("Nowy balans", $"**{finalData.Credits}** 💳", true)
                .WithColor(win ? Color.Gold : Color.DarkRed)
                .WithThumbnailUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                .WithFooter($"Wywołano przez {Context.User.Username}", Context.User.GetAvatarUrl())
                .WithCurrentTimestamp()
                .Build();

            await msg.ModifyAsync(m => m.Embed = embed);
        }

        // =========================================================
        // RULETKA — COMMAND
        // =========================================================
        [SlashCommand("ruletka", "Postaw zakład na kolor.")]
        public async Task Ruletka(int stawka)
        {
            if (stawka <= 0)
            {
                await RespondAsync(embed: Error("Kwota musi być > 0."), ephemeral: true);
                return;
            }

            var data = await UserDataManager.GetUserAsync(Context.User.Id);
            if (data.Credits < stawka)
            {
                await RespondAsync(embed: Error("Za mało kredytów!"), ephemeral: true);
                return;
            }

            RouletteStakes[Context.User.Id] = stawka;

            var buttons = new ComponentBuilder()
                .WithButton("🔴 Czerwony", "roulette_red", ButtonStyle.Danger)
                .WithButton("⚫ Czarny", "roulette_black", ButtonStyle.Secondary)
                .WithButton("🟩 Zielony (0)", "roulette_green", ButtonStyle.Success);

            await RespondAsync(
                embed: new EmbedBuilder()
                    .WithTitle("🎡 Ruletka")
                    .WithDescription($"Stawiasz **{stawka}** kredytów.\n\nWybierz kolor poniżej i spróbuj szczęścia!")
                    .AddField("Wypłaty", "🟩 **Zielony (0)** — x14\n🔴 **Czerwony** — x2\n⚫ **Czarny** — x2", false)
                    .WithColor(Color.Blue)
                    .WithThumbnailUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                    .WithFooter($"Wywołano przez {Context.User.Username}", Context.User.GetAvatarUrl())
                    .WithCurrentTimestamp()
                    .Build(),
                components: buttons.Build()
            );
        }

        // =========================================================
        // RULETKA — STATIC BUTTON HANDLER (FIXED)
        // =========================================================
        public static async Task HandleRouletteButtonsStatic(SocketMessageComponent component)
        {
            if (!component.Data.CustomId.StartsWith("roulette_"))
                return;

            // ensure interaction is acknowledged
            if (!component.HasResponded)
                await component.DeferAsync();

            ulong uid = component.User.Id;

            // no active stake?
            if (!RouletteStakes.TryGetValue(uid, out int stawka))
            {
                await component.FollowupAsync(embed:
                    new EmbedBuilder()
                        .WithTitle("❌ Błąd")
                        .WithDescription("Nie masz aktywnej ruletki. Użyj ponownie komendy `/ruletka`.")
                        .WithColor(Color.DarkRed)
                        .WithFooter($"Gracz: {component.User.Username}", component.User.GetAvatarUrl())
                        .WithCurrentTimestamp()
                        .Build(),
                    ephemeral: true);
                return;
            }

            string choice = component.Data.CustomId.Replace("roulette_", "");

            var rand = new Random();
            int finalNum = rand.Next(0, 37);
            string finalColor = finalNum == 0 ? "green" :
                                finalNum % 2 == 0 ? "black" : "red";

            // send spinning message
            var msg = await component.FollowupAsync(embed:
                new EmbedBuilder()
                    .WithDescription("🎡 Kula się kręci...")
                    .WithColor(Color.DarkGrey)
                    .WithFooter($"Gracz: {component.User.Username}", component.User.GetAvatarUrl())
                    .WithCurrentTimestamp()
                    .Build()
            ) as IUserMessage;

            // spinning animation
            foreach (int n in Enumerable.Range(0, 12).Select(_ => rand.Next(0, 37)).Append(finalNum))
            {
                string col = n == 0 ? "🟩" : (n % 2 == 0 ? "⚫" : "🔴");

                var embedStep = new EmbedBuilder()
                    .WithDescription($"🎲 {col} {n}")
                    .WithColor(Color.DarkGrey)
                    .WithFooter($"Gracz: {component.User.Username}", component.User.GetAvatarUrl())
                    .WithCurrentTimestamp()
                    .Build();

                await msg.ModifyAsync(m => { m.Embed = embedStep; });
                await Task.Delay(120);
            }

            // determine reward
            bool win = choice == finalColor;
            int reward =
                finalColor == "green" ? stawka * 14 :
                win ? stawka * 2 : 0;

            // apply win/loss
            if (win)
                await UserDataManager.AddCreditsAsync(uid, reward);
            else
                await UserDataManager.RemoveCreditsAsync(uid, stawka);

            RouletteStakes.Remove(uid);

            // get balance BEFORE ModifyAsync
            var finalData = await UserDataManager.GetUserAsync(uid);

            var finalEmbed = new EmbedBuilder()
                .WithTitle("🎯 Wynik ruletki")
                .WithDescription(
                    $"Wypadło **{finalNum}** ({finalColor}).\n\n" +
                    (win ? $"🎉 WYGRAŁEŚ **{reward}** kredytów!" :
                           $"💀 PRZEGRAŁEŚ **{stawka}** kredytów.")
                )
                .AddField("Nowy balans", $"**{finalData.Credits}** 💳", true)
                .WithColor(win ? Color.Green : Color.Red)
                .WithFooter($"Gracz: {component.User.Username}", component.User.GetAvatarUrl())
                .WithCurrentTimestamp()
                .Build();

            await msg.ModifyAsync(m => { m.Embed = finalEmbed; });
        }

        // =========================================================
        // BET
        // =========================================================
        [SlashCommand("bet", "50/50 — podwój stawkę.")]
        public async Task Bet(int amount)
        {
            if (amount <= 0)
            {
                await RespondAsync(embed: Error("Kwota musi być > 0."), ephemeral: true);
                return;
            }

            var data = await UserDataManager.GetUserAsync(Context.User.Id);

            if (data.Credits < amount)
            {
                await RespondAsync(embed: Error("Masz za mało kredytów."), ephemeral: true);
                return;
            }

            bool win = new Random().NextDouble() < 0.5;

            if (win)
                await UserDataManager.AddCreditsAsync(Context.User.Id, amount);
            else
                await UserDataManager.RemoveCreditsAsync(Context.User.Id, amount);

            data = await UserDataManager.GetUserAsync(Context.User.Id);

            await RespondAsync(embed:
                new EmbedBuilder()
                    .WithTitle(win ? "🎉 Wygrana!" : "💀 Przegrana!")
                    .WithDescription(win
                        ? $"Udało Ci się podwoić **{amount}** kredytów!"
                        : $"Straciłeś **{amount}** kredytów w zakładzie 50/50.")
                    .AddField("Nowy balans", $"**{data.Credits}** 💳", true)
                    .WithColor(win ? Color.Gold : Color.DarkRed)
                    .WithFooter($"Wywołano przez {Context.User.Username}", Context.User.GetAvatarUrl())
                    .WithCurrentTimestamp()
                    .Build()
            );
        }

        // =========================================================
        // LEADERBOARD
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

            string Medal(int i) =>
                i == 0 ? "🥇" :
                i == 1 ? "🥈" :
                i == 2 ? "🥉" : "🎰";

            string text = string.Join("\n",
                list.Select((x, i) =>
                    $"{Medal(i)} **#{i + 1}** — <@{x.UserId}>  \n  💰 **{x.Credits}** kredytów"));

            await FollowupAsync(embed:
                new EmbedBuilder()
                    .WithTitle("🏆 TOP 10 GRACZY")
                    .WithDescription(text)
                    .WithColor(Color.Gold)
                    .WithFooter($"Wywołano przez {Context.User.Username}", Context.User.GetAvatarUrl())
                    .WithCurrentTimestamp()
                    .Build()
            );
        }

        // =========================================================
        // DAILY
        // =========================================================
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

            await FollowupAsync(embed:
                new EmbedBuilder()
                    .WithTitle("🎁 Nagroda dzienna")
                    .WithDescription("Dziękujemy za codzienną aktywność!")
                    .AddField("Dzisiejsza nagroda", $"✨ **{reward}** kredytów", true)
                    .AddField("Nowy balans", $"💳 **{data.Credits}**", true)
                    .WithColor(Color.Green)
                    .WithThumbnailUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                    .WithFooter($"Wywołano przez {Context.User.Username}", Context.User.GetAvatarUrl())
                    .WithCurrentTimestamp()
                    .Build()
            );
        }

        // =========================================================
        // ADMIN GRANT
        // =========================================================
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

            await RespondAsync(embed:
                new EmbedBuilder()
                    .WithTitle("🛠 ADMIN — przyznano kredyty")
                    .WithDescription($"Przyznano **{amount}** kredytów użytkownikowi {target.Mention}.")
                    .AddField("Nowy balans użytkownika", $"**{data.Credits}** 💳", true)
                    .WithColor(Color.Blue)
                    .WithFooter($"Akcja wykonana przez {Context.User.Username}", Context.User.GetAvatarUrl())
                    .WithCurrentTimestamp()
                    .Build(),
                ephemeral: true
            );
        }
    }
}
