using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using System.Threading.Tasks;

namespace DSharpBotCore.Extensions
{
    public static class CommandContextExtensions
    {
        public static async Task ErrorWith(this CommandContext ctx, Bot bot, string title = null, string description = null, params (string item, string desc)[] moreInfo)
        {
            var embed = new DiscordEmbedBuilder()
                .WithColor(new DiscordColor(0xFF0000))
                .WithDefaultFooter(bot)
                .WithTitle(title ?? "Error")
                .WithDescription(description ?? "Error");

            foreach (var item in moreInfo)
                embed.AddField(item.item ?? "More Info", item.desc ?? " ? ? ? ", bot.Config.Errors.InlineInfo);

            var msg = await ctx.RespondAsync(embed: embed);
            await Task.Delay(bot.Config.Errors.PersistTime);
            await msg.DeleteAsync();
        }
    }
}
