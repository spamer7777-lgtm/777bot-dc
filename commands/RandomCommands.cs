using System;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;

namespace Commands
{
    [CommandContextType(InteractionContextType.BotDm, InteractionContextType.PrivateChannel, InteractionContextType.Guild)]
    [IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
    [Group("random", "All random (RNG) commands.")]
    public class RandomGroup : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly Random random = new();

        [SlashCommand("coin-toss", "Flip a coin")]
        public async Task CoinToss()
        {
            int randInt = random.Next(0, 2);
            string result;
            if (randInt == 1)
            {
                result = "heads";
            }
            else
            {
                result = "tails";
            }
            await RespondAsync(text: $"🪙 The coin landed **" + result + "**!");
        }

        [SlashCommand("dice-roll", "Roll a 6 sided die.")]
        public async Task DiceRoll()
        {
            int randInt = random.Next(1, 7);
            await RespondAsync(text: $"🎲 The die landed on **{randInt}**");
        }
    }
}