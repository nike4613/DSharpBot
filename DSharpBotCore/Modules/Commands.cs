using DSharpPlus.CommandsNext;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext.Attributes;
using System;
using DSharpPlus.Interactivity;
using DSharpPlus.Entities;
using System.Collections.Generic;
using System.Linq;
using DSharpBotCore.Extensions;
using DSharpBotCore.Entities;

namespace DSharpBotCore.Modules
{
    class Commands : BaseCommandModule
    {
        Bot bot;
        Configuration config;
        public Commands(Bot bot, Configuration config)
        {
            this.bot = bot;
            this.config = config;
        }

        [Command("hi"), Hidden]
        [Description("Responds with a nice welcoming message.")]
        public async Task Hi(CommandContext ctx)
        {
            await ctx.RespondAsync($"👋 Hi, {ctx.User.Mention}!");
        }

        [Command("poll")]
        [Priority(1)]
        [Description("Call a poll on something")]
        [RequirePermissions(DSharpPlus.Permissions.Administrator)]
        public async Task Poll(CommandContext ctx, 
            [Description("The time length of the poll.")] TimeSpan duration,
            [Description("The text to show in the poll, followed by ';;' then a ';' seperated repsonses to listen for.")]
            [RemainingText] string pollText = null)
        {
            // Init info
            await ctx.TriggerTypingAsync();
            var interactivity = ctx.Client.GetInteractivity();
            var pollAuthor = ctx.Message.Author;
            if (config.Commands.Poll.DeleteTrigger)
                await ctx.Message.DeleteAsync("Removing message to keep logs clear");

            if (pollText == null)
            {
                await ctx.ErrorWith(bot, "Not enough options were provided.", "No poll text was provided.", (null, "Argument 2 (pollText) wasn't provided."));
                return;
            }

            // Parse arguments
            string[] textParts = Array.ConvertAll(pollText.Split(";;", StringSplitOptions.RemoveEmptyEntries), s=>s.Trim());

            if (textParts.Length < 2)
            { // Show error message for configured time
                await ctx.ErrorWith(bot, "Not enough options were provided.", "No response options were provided.", (null, "Argument 2 (pollText) wasn't in the correct format."));
                return;
            }

            var text = textParts[0];
            var options = Array.ConvertAll(textParts[1].Split(";", StringSplitOptions.RemoveEmptyEntries), s=>s.Trim());

            if (options.Length < 1)
            {
                await ctx.ErrorWith(bot, "Not enough options were provided.", "No response options were provided.");
                return;
            }

            // Construct embed
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Poll time!")
                .WithDescription($"{text}\n\nSay your response in this chat to vote!\nYou have **{duration.ToReadable()}**")
                .WithColor(DiscordColor.Aquamarine)
                .WithUserAsAuthor(pollAuthor)
                .WithDefaultFooter(bot);

            string descformat = "Respond with `{0}` to vote!\n**Votes:** `{1}`";

            string optionTransform(string s) => new string(s.ToLower().Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());

            var responses = new Dictionary<string, (int Votes, int Index)>();
            foreach (var option in options)
            {
                var optname = optionTransform(option);
                if (!responses.ContainsKey(optname))
                    responses[optname] = (Votes: 0, Index: Array.IndexOf(options, option));

                embed.AddField(
                    name: option, 
                    value: String.Format(descformat, optname, responses[optname].Votes),
                    inline: true
                );
            }

            // Send message and wait for responses
            var message = await ctx.RespondAsync(embed: embed);
            await interactivity.WaitForMessageAsync((msg) =>
            {
                var cont = optionTransform(msg.Content);
                if (responses.ContainsKey(cont))
                {
                    var (Votes, Index) = responses[cont];
                    Votes++;
                    responses[cont] = (Votes, Index);
                    embed.Fields[Index].Value = String.Format(descformat, cont, responses[cont].Votes);
                    message.ModifyAsync(embed: embed.Build());

                    if (config.Commands.Poll.DeleteResponses)
                        msg.DeleteAsync();
                }
                return false; // never accept message
            }, duration);

            // Process results
            await ctx.TriggerTypingAsync();
            var resultsEmbed = new DiscordEmbedBuilder()
                .WithTitle($"The results for `{text}` are in!")
                .WithColor(DiscordColor.Cyan)
                .WithUserAsAuthor(pollAuthor)
                .WithDefaultFooter(bot);

            int totalVotes = responses.Values.Aggregate((a, b) => (a.Votes + b.Votes, -1)).Votes;
            if (totalVotes == 0)
            {
                var emoji = DiscordEmoji.FromName(ctx.Client, ":frowning2:");
                resultsEmbed.Description = $"Nobody voted. {emoji}";
            }
            else
            {
                var groups = responses.OrderByDescending(x => x.Value.Votes).GroupBy(x => x.Value.Votes).Select(x => x.Key).ToArray();
                var ordered = responses.Select(x => x.Value).OrderBy(x => Array.IndexOf(groups, x.Votes));

                var first = new List<(int Votes, int Index)>();

                foreach (var (Votes, Index) in ordered)
                {
                    var placement = Array.IndexOf(groups, Votes)+1;
                    var name = options[Index];

                    if (placement == 1)
                        first.Add((Votes, Index));

                    if (placement <= 3) // only show the top 3 places (including duplicate placements)
                        resultsEmbed.AddField(
                            name: $"#{placement}: `{name}`",
                            value: $"`{name}` placed #{placement} with `{Votes}` votes!",
                            inline: false
                        );
                    else
                        break;
                }

                if (first.Count == 1)
                    resultsEmbed.Description = $"The winner is `{options[first[0].Index]}` with `{first[0].Votes}` votes!";
                else
                    resultsEmbed.Description = $"The winners are {first.Select(x => $"`{options[x.Index]}`").MakeReadableString()} with `{first[0].Votes}` votes!";
            }

            await message.DeleteAsync();
            await ctx.RespondAsync(embed: resultsEmbed);
        }

        [Command("poll")]
        [Priority(0)]
        [RequirePermissions(DSharpPlus.Permissions.Administrator)]
        public async Task PollNoArgs(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();
            if (config.Commands.Poll.DeleteTrigger)
                await ctx.Message.DeleteAsync("Removing message to keep logs clear");
            await ctx.ErrorWith(bot, "Not enough options were provided.", "No poll time was provided.", (null, "Argument 1 (duration) wasn't provided."));
        }

        [Command("d6")]
        [Description("Alias for `roll 6`")]
        public async Task RollD6(CommandContext ctx, 
            [Description("The number of dice to use.")] int number = 1) => await RollDice(ctx, 6, number);

        [Command("roll"), Aliases("r","d")]
        [Description("Rolls *n* *n*-sided dice and shows the result.")]
        public async Task RollDice(CommandContext ctx, 
            [Description("The number of faces to use.")] int? faces = null, 
            [Description("The number of dice to use.")] int number = 1)
        {
            await ctx.TriggerTypingAsync();

            if (config.Commands.Roll.DeleteTrigger)
                await ctx.Message.DeleteAsync();

            if (faces == null)
            {
                await ctx.ErrorWith(bot, "Not enough options were provided.", "Number of faces was not specified.", (null, "Argument 1 (faces) wasn't provided."));
                return;
            }

            var embed = new DiscordEmbedBuilder()
                .WithUserAsAuthor(ctx.Message.Author)
                .WithDefaultFooter(bot)
                .WithTitle($"Rolling {number}d{faces}");

            if (config.Commands.Roll.D6.UseSpecial && faces == 6)
            { // special D6 handling

            }

            var random = new Random();
            var results = Enumerable.Repeat(0, number).Select(_ => random.Next(1, faces.Value + 1)).ToArray();

            embed.Description = $"The results: {results.Select(x => $"`{x}`").MakeReadableString()}.\nThe sum: `{results.Sum()}`.";

            await ctx.RespondAsync(embed: embed);
        }
    }
}
