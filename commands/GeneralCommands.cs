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

        // 🎰 New Slots command
        [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
        [IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
        [SlashCommand("slots", "Try your luck at 777 Slots!")]
        public async Task Slots()
        {
            string[] icons = { "🍒", "🍋", "🍉", "💎", "7️⃣" };
            var rand = new Random();

            // Generate 3 random slot symbols
            var result = Enumerable.Range(0, 3)
                .Select(_ => icons[rand.Next(icons.Length)])
                .ToArray();

            string output = string.Join(" ", result);
            bool win = result.Distinct().Count() == 1;

            var embed = new EmbedBuilder()
                .WithTitle("🎰 777 Slots 🎰")
                .WithDescription($"**{output}**\n" +
                                 (win ? "💰 **JACKPOT! WYGRAŁEŚ/AŚ!**" : "😢 Następnym razem odda..."))
                .WithColor(win ? Color.Gold : Color.DarkGrey)
                .WithFooter($"Played by {Context.User.Username}", Context.User.GetAvatarUrl())
                .Build();

            await RespondAsync(embed: embed);
        }
    }
}
