using DSharpPlus.CommandsNext;
using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpBotCore.Extensions;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpBotCore.Entities;

namespace DSharpBotCore.Modules.Modes
{
    internal class IconsDndCommands : BaseCommandModule
    {
        private readonly Bot bot;

        public IconsDndCommands(Bot bot, Configuration config)
        {
            this.bot = bot;
            Config = config;
        }

        public Configuration Config { get; set; }

        [Command("d6")]
        [Description("Alias for `roll 6`")]
        public async Task RollD6(CommandContext ctx,
            [Description("The number of dice to use.")] int number = 1) => await RollDice(ctx, 6, number);

        [Command("roll"), Aliases("r", "d")]
        [Description("Rolls *n* *n*-sided dice and shows the result.")]
        public async Task RollDice(CommandContext ctx,
            [Description("The number of faces to use.")] int? faces = null,
            [Description("The number of dice to use.")] int number = 1)
        {
            await ctx.TriggerTypingAsync();

            if (Config.Commands.Roll.DeleteTrigger)
                await ctx.Message.DeleteAsync();

            var authMember = await ctx.Guild.GetMemberAsync(ctx.Message.Author.Id);

            if (faces == null)
            {
                await ctx.ErrorWith(bot, "Not enough options were provided.", "Number of faces was not specified.", (null, "Argument 1 (faces) wasn't provided."));
                return;
            }

            var embed = new DiscordEmbedBuilder()
                .WithMemberAsAuthor(authMember)
                .WithDefaultFooter(bot)
                .WithTitle($"Rolling {number}d{faces}");

            if (Config.Commands.Roll.D6.UseSpecial && faces == 6)
            { // special D6 handling

            }

            var random = new Random();
            var results = Enumerable.Repeat(0, number).Select(_ => random.Next(1, faces.Value + 1)).ToArray();
            var sum = results.Sum();

            embed.Description = $"The results: {results.Select(x => $"`{x}`").MakeReadableString()}.\nThe sum: `{sum}`.";

            if (!Commands.UserLastResults.ContainsKey(authMember))
                Commands.UserLastResults.Add(authMember, sum);
            else
                Commands.UserLastResults[authMember] = sum;

            await ctx.RespondAsync(embed: embed);
        }
    }
}
