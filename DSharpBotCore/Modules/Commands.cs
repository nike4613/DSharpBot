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
using System.Threading;
using org.mariuszgromada.math.mxparser;

namespace DSharpBotCore.Modules
{
    public class Commands : BaseCommandModule
    {
        Bot bot;
        Configuration config;
        CancellationTokenSource cts;
        public Commands(Bot bot, Configuration config, CancellationTokenSource cts)
        {
            this.bot = bot;
            this.config = config;
            this.cts = cts;
        }

        [Command("hi"), Hidden]
        [Description("Responds with a nice welcoming message.")]
        public async Task Hi(CommandContext ctx)
        {
            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            await ctx.RespondAsync($"👋 Hi, {ctx.User.Mention}!");
        }

        [Command("poll")]
        [Priority(1)]
        [Description("Call a poll on something")]
        [RequirePermissions(DSharpPlus.Permissions.Administrator)]
        public async Task Poll(CommandContext ctx, 
            [Description("The time length of the poll.")] TimeSpan duration,
            [Description("The text to show in the poll, followed by ';;' then a ';' seperated repsonses to listen for.")]
            [RemainingText] string pollText)
        {
            // Init info
            await ctx.TriggerTypingAsync();
            var interactivity = ctx.Client.GetInteractivity();
            var pollAuthor = ctx.Message.Author;
            if (config.Commands.Poll.DeleteTrigger)
                await ctx.Message.DeleteAsync("Removing message to keep logs clear");

            var pollAuthMember = await ctx.Guild.GetMemberAsync(pollAuthor.Id);

            if (pollText == null)
            {
                await ctx.ErrorWith(bot, "Not enough options were provided.", "No poll text was provided.", (null, "Argument 2 (pollText) wasn't provided."));
                return;
            }

            // Parse arguments
            string[] textParts = Array.ConvertAll(pollText.Split(new[] { ";;" }, StringSplitOptions.RemoveEmptyEntries), s=>s.Trim());

            if (textParts.Length < 2)
            { // Show error message for configured time
                await ctx.ErrorWith(bot, "Not enough options were provided.", "No response options were provided.", (null, "Argument 2 (pollText) wasn't in the correct format."));
                return;
            }

            var text = textParts[0];
            var options = Array.ConvertAll(textParts[1].Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries), s=>s.Trim());

            if (options.Length < 1)
            {
                await ctx.ErrorWith(bot, "Not enough options were provided.", "No response options were provided.");
                return;
            }

            var description = $"{text}\n\nSay your response in this chat to vote!\nYou have **{{0}}**";

            // Construct embed
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Poll time!")
                .WithDescription(String.Format(description, duration.ToReadable()))
                .WithColor(DiscordColor.Aquamarine)
                .WithMemberAsAuthor(pollAuthMember)
                .WithDefaultFooter(bot);

            string descformat = "Respond with `{0}` to vote!\n**Votes:** `{1}`";

            string OptionTransform(string s) => new string(s.ToLower().Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());

            var responses = new Dictionary<string, (int Votes, int Index)>();
            foreach (var option in options)
            {
                var optname = OptionTransform(option);
                if (!responses.ContainsKey(optname))
                    responses[optname] = (Votes: 0, Index: Array.IndexOf(options, option));

                embed.AddField(
                    name: option, 
                    value: String.Format(descformat, optname, responses[optname].Votes),
                    inline: true
                );
            }

            var startTime = DateTime.Now;

            // Send message and wait for responses
            var message = await ctx.RespondAsync(embed: embed);
            var messageTask = interactivity.WaitForMessageAsync(msg =>
            {
                var cont = OptionTransform(msg.Content);
                if (responses.ContainsKey(cont))
                {
                    var (votes, index) = responses[cont];
                    votes++;
                    responses[cont] = (votes, index);
                    embed.Fields[index].Value = string.Format(descformat, cont, responses[cont].Votes);

                    var elapsed = DateTime.Now - startTime;
                    var remaining = duration - elapsed;
                    embed.Description = string.Format(description, remaining.ToReadable());
                    
                    message.ModifyAsync(embed: embed.Build());

                    if (config.Commands.Poll.DeleteResponses)
                        msg.DeleteAsync();
                }
                return false; // never accept message
            }, duration);

            if (config.Commands.Poll.UpdateTime)
                while (!messageTask.Wait(TimeSpan.FromSeconds(5)))
                { // tick every second
                    var elapsed = DateTime.Now - startTime;
                    var remaining = duration - elapsed;
                    embed.Description = string.Format(description, remaining.ToReadable());

                    await message.ModifyAsync(embed: embed.Build());
                }
            else
                await messageTask;

            // Process results
            await ctx.TriggerTypingAsync();
            var resultsEmbed = new DiscordEmbedBuilder()
                .WithTitle($"The results for `{text}` are in!")
                .WithColor(DiscordColor.Cyan)
                .WithMemberAsAuthor(pollAuthMember)
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

                foreach (var (votes, index) in ordered)
                {
                    var placement = Array.IndexOf(groups, votes)+1;
                    var name = options[index];

                    if (placement == 1)
                        first.Add((votes, index));

                    if (placement <= 3) // only show the top 3 places (including duplicate placements)
                        resultsEmbed.AddField(
                            $"#{placement}: `{name}`",
                            $"`{name}` placed #{placement} with `{votes}` votes!"
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

        [Command("poll")]
        [Priority(0)]
        [RequirePermissions(DSharpPlus.Permissions.Administrator)]
        public async Task PollNoTimespan(CommandContext ctx, [RemainingText] string pollText)
        {
            await ctx.TriggerTypingAsync();
            if (config.Commands.Poll.DeleteTrigger)
                await ctx.Message.DeleteAsync("Removing message to keep logs clear");
            await ctx.ErrorWith(bot, "Not enough options were provided.", "No poll time was provided.", (null, "Argument 1 (duration) wasn't provided."));
        }

        internal static readonly Dictionary<DiscordMember, double> UserLastResults = new Dictionary<DiscordMember, double>();
        [Command("calc"), Aliases("c")]
        [Description("Evaluates a provided expression")]
        public async Task EvalExpression(CommandContext ctx,
            [Description("The expression to evaluate"), RemainingText] string expr)
        {
            await ctx.TriggerTypingAsync();

            if (config.Commands.Calc.DeleteTrigger)
                await ctx.Message.DeleteAsync();

            var exprObj = new Expression(expr);
            var authMember = await ctx.Guild.GetMemberAsync(ctx.Message.Author.Id);

            if (UserLastResults.ContainsKey(authMember))
            {
                exprObj.defineConstant("x", UserLastResults[authMember]);
            }

            if (!exprObj.checkSyntax())
            {
                await ctx.ErrorWith(bot, "Invalid expression syntax", exprObj.getErrorMessage());
                return;
            }

            var embed = new DiscordEmbedBuilder()
                .WithMemberAsAuthor(authMember)
                .WithDefaultFooter(bot)
                .WithTitle($"Expression `{exprObj.getExpressionString()}`");

            var result = exprObj.calculate();

            if (!UserLastResults.ContainsKey(authMember))
                UserLastResults.Add(authMember, result);
            else
                UserLastResults[authMember] = result;

            embed.Description = $"`{exprObj.getExpressionString()} = {result}`";

            await ctx.RespondAsync(embed: embed);
        }

        [Command("exit"), Hidden, RequireOwner]
        [Description("Causes the bot to quit.")]
        public async Task QuitBot(CommandContext ctx)
        {
            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");
            cts.Cancel();
        }
    }
}
