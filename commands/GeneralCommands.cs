using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

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

        [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
        [IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
        [SlashCommand("balance", "Check your current credits.")]
        public async Task Balance()
        {
            var user = UserDataManager.GetUser(Context.User.Id);
            var embed = new EmbedBuilder()
                .WithTitle($"Balans: 💰 {Context.User.Username}")
                .WithDescription($"Masz **{user.Credits}** kredtyów.")
                .WithColor(Color.Gold)
                .Build();

            await RespondAsync(embed: embed);
        }

[CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
[IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
[SlashCommand("slots", "Sprawdź swoje szczęście")]
public async Task Slots()
{
    const int cost = 10;
    const int reward = 50;

    var user = UserDataManager.GetUser(Context.User.Id);

    if (user.Credits < cost)
    {
        await RespondAsync($"🚫 Potrzebujesz {cost} kredytów, żeby zagrać. Aktualnie masz ich: {user.Credits}.");
        return;
    }

    // Deduct cost
    UserDataManager.RemoveCredits(Context.User.Id, cost);

    string[] icons = { "🍒", "🍋", "🍉", "💎", "7️⃣" };
    var rand = new Random();

    // Send initial "spinning" message
    var msg = await RespondAsync("🎰 | [⬜][⬜][⬜] Kręcimy...");

    // Animate reels 5 times (~2 seconds)
    for (int i = 0; i < 5; i++)
    {
        var spin = Enumerable.Range(0, 3)
            .Select(_ => icons[rand.Next(icons.Length)])
            .ToArray();

        string spinContent = $"🎰 | [{spin[0]}][{spin[1]}][{spin[2]}] Kręcimy...";
        await msg.ModifyAsync(m => m.Content = spinContent);
        await Task.Delay(400);
    }

    // Final result
    var finalResult = Enumerable.Range(0, 3)
        .Select(_ => icons[rand.Next(icons.Length)])
        .ToArray();

    bool win = finalResult.Distinct().Count() == 1;
    if (win) UserDataManager.AddCredits(Context.User.Id, reward);

    string resultText = $"🎰 | [{finalResult[0]}][{finalResult[1]}][{finalResult[2]}]\n" +
                        (win ? $"💰 **JACKPOT! WYGRAŁEŚ/AŚ {reward} kredytów!**" :
                               $"😢 Przegrałeś/aś {cost} kredytów. Następnym razem lepiej!");

    // Update message with final result and balance
    await msg.ModifyAsync(m => m.Content = resultText +
        $"\n💳 Twój nowy balans: {UserDataManager.GetUser(Context.User.Id).Credits} kredytów");
}


        // 🛠️ Hidden Admin Command
        [SlashCommand("grantcredits", "Admin only: give credits to a user (hidden).")]
        [DefaultMemberPermissions(GuildPermission.Administrator)] // require admin permission
        [CommandContextType(InteractionContextType.Guild)] // guild only
        [IntegrationType(ApplicationIntegrationType.GuildInstall)] // local command, not global
        public async Task GrantCredits(
            [Summary("user", "The user to give credits to.")] IUser target,
            [Summary("amount", "The amount of credits to add.")] int amount)
        {
            // optional: restrict by specific user ID
            ulong ownerId = 299929951451217921; // 🔒 your Discord ID here
            if (Context.User.Id != ownerId && !((SocketGuildUser)Context.User).GuildPermissions.Administrator)
            {
                await RespondAsync("🚫 You are not authorized to use this command.", ephemeral: true);
                return;
            }

            if (amount <= 0)
            {
                await RespondAsync("⚠️ Amount must be greater than 0.", ephemeral: true);
                return;
            }

            UserDataManager.AddCredits(target.Id, amount);
            var newBalance = UserDataManager.GetUser(target.Id).Credits;

            await RespondAsync(
                $"✅ Added **{amount}** credits to {target.Mention}. New balance: **{newBalance}** credits.",
                ephemeral: true // hidden response
            );
        }
    }
}


