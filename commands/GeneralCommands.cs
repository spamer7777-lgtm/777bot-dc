using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;

namespace Commands
{
    public class NoGroup : InteractionModuleBase<SocketInteractionContext>
    {
        [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
        [IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
        [SlashCommand("ping", "See the bot's ping.")]
        public async Task Ping()
        {
            await RespondAsync(text: $"🏓 Pong! The client latency is **{Bot.Client.Latency}** ms.");
        }

        [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
        [IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
        [SlashCommand("hi", "Powiedz Siemano!")]
        public async Task Hi([Summary("user", "The user to say hi to.")] IUser user)
        {
            await RespondAsync(text: $"👋 HEEEJ! {user.Mention}!");
        }

        // 💰 Show balance command
        [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
        [IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
        [SlashCommand("balance", "Sprawdź ile masz kredytów.")]
        public async Task Balance()
        {
            var user = UserDataManager.GetUser(Context.User.Id);
            var embed = new EmbedBuilder()
                .WithTitle($"Balans💰 {Context.User.Username}")
                .WithDescription($"Masz **{user.Credits}** kredytów.")
                .WithColor(Color.Gold)
                .Build();

            await RespondAsync(embed: embed);
        }

        // 🎰 Slots command with credit system
        [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
        [IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
        [SlashCommand("slots", "Sprawdz swoje szczescie, zakręc jednorękim bandytą!")]
        public async Task Slots()
        {
            const int cost = 10;
            const int reward = 50;

            var user = UserDataManager.GetUser(Context.User.Id);

            if (user.Credits < cost)
            {
                await RespondAsync($"🚫 Potrzebujesz {cost} kredtyów żeby zagrać. W tym momencie masz {user.Credits}.");
                return;
            }

            // Deduct the cost
            UserDataManager.RemoveCredits(Context.User.Id, cost);

            string[] icons = { "🍒", "🍋", "🍉", "💎", "7️⃣" };
            var rand = new Random();
            var result = Enumerable.Range(0, 3).Select(_ => icons[rand.Next(icons.Length)]).ToArray();

            string output = string.Join(" ", result);
            bool win = result.Distinct().Count() == 1;

            if (win)
                UserDataManager.AddCredits(Context.User.Id, reward);

            var embed = new EmbedBuilder()
                .WithTitle("🎰 777 Slots 🎰")
                .WithDescription($"**{output}**\n" +
                                 (win ? $"💰 **JACKPOT! WYGRAŁEŚ/AŚ {reward} kredytów!**" :
                                         $"😢 Straciłeś/aś {cost} kredtyów. Następnym razem napewno odda..."))
                .WithColor(win ? Color.Gold : Color.DarkGrey)
                .WithFooter($"Posiadasz: {UserDataManager.GetUser(Context.User.Id).Credits} kredytów")
                .Build();

            await RespondAsync(embed: embed);
        }
    }
}

