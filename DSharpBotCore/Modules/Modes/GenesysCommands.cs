using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpBotCore.Entities;
using DSharpBotCore.Extensions;
using DSharpBotCore.Modules.Modes.Genesys;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;

namespace DSharpBotCore.Modules.Modes
{
    internal class GenesysCommands : BaseCommandModule
    {
        private readonly Bot bot;
        private readonly Dictionary<Symbol, DiscordEmoji> symbolEmoji = new Dictionary<Symbol, DiscordEmoji>();
        private readonly Dictionary<DiscordEmoji, Dice.DiceType> emojiDice = new Dictionary<DiscordEmoji, Dice.DiceType>();
        private readonly List<DiscordEmoji> emojiDiceOrder = new List<DiscordEmoji>();

        private DiscordEmoji confirmEmoji;
        private DiscordEmoji cancelEmoji;

        public GenesysCommands(Bot bot, Configuration config)
        {
            this.bot = bot;
            Config = config;
            bot.Client.GuildAvailable += async args =>
            {
                var symbols = config.Commands.Roll.GenesysSymbols;
                symbolEmoji[Symbol.None] = await args.Guild.GetEmojiAsync(symbols.None);
                symbolEmoji[Symbol.Success] = await args.Guild.GetEmojiAsync(symbols.Success);
                symbolEmoji[Symbol.Advantage] = await args.Guild.GetEmojiAsync(symbols.Advantage);
                symbolEmoji[Symbol.Triumph] = await args.Guild.GetEmojiAsync(symbols.Triumph);
                symbolEmoji[Symbol.Failure] = await args.Guild.GetEmojiAsync(symbols.Failure);
                symbolEmoji[Symbol.Threat] = await args.Guild.GetEmojiAsync(symbols.Threat);
                symbolEmoji[Symbol.Despair] = await args.Guild.GetEmojiAsync(symbols.Despair);
            };
            bot.Client.GuildAvailable += async args =>
            {
                var symbols = config.Commands.Roll.GenesysDice;
                var emoji = await args.Guild.GetEmojiAsync(symbols.Boost);
                emojiDice[emoji] = Dice.DiceType.Boost;
                emojiDiceOrder.Add(emoji);
                emoji = await args.Guild.GetEmojiAsync(symbols.Ability);
                emojiDice[emoji] = Dice.DiceType.Ability;
                emojiDiceOrder.Add(emoji);
                emoji = await args.Guild.GetEmojiAsync(symbols.Proficiency);
                emojiDice[emoji] = Dice.DiceType.Proficiency;
                emojiDiceOrder.Add(emoji);
                emoji = await args.Guild.GetEmojiAsync(symbols.Setback);
                emojiDice[emoji] = Dice.DiceType.Setback;
                emojiDiceOrder.Add(emoji);
                emoji = await args.Guild.GetEmojiAsync(symbols.Difficulty);
                emojiDice[emoji] = Dice.DiceType.Difficulty;
                emojiDiceOrder.Add(emoji);
                emoji = await args.Guild.GetEmojiAsync(symbols.Challenge);
                emojiDice[emoji] = Dice.DiceType.Challenge;
                emojiDiceOrder.Add(emoji);
            };
            bot.Client.GuildAvailable += args =>
            {
                var src = config.Commands.Roll.Reactions;
                confirmEmoji = DiscordEmoji.FromName(args.Client, src.Confirm);
                cancelEmoji = DiscordEmoji.FromName(args.Client, src.Cancel);
                return Task.CompletedTask;
            };
        }

        public Configuration Config { get; set; }

        [Command("roll"), Aliases("r")]
        [Description("Rolls Genesys dice.")]
        public async Task RollDice(CommandContext ctx)
        {
            try
            {
                var interact = ctx.Client.GetInteractivity();

                await ctx.TriggerTypingAsync();

                if (Config.Commands.Roll.DeleteTrigger)
                    await ctx.Message.DeleteAsync();

                var authMember = await ctx.Guild.GetMemberAsync(ctx.Message.Author.Id);
                
                var embedBase = new DiscordEmbedBuilder()
                    .WithMemberAsAuthor(authMember)
                    .WithDefaultFooter(bot);
                var beforeRollEmbed = new DiscordEmbedBuilder(embedBase);
                beforeRollEmbed.WithTitle("Select Dice");

                var toRollDice = new Dictionary<Dice.DiceType, int>();
                foreach (var emoji in emojiDiceOrder)
                {
                    toRollDice.Add(emojiDice[emoji], 0);
                    beforeRollEmbed.AddField(emoji, "0", true);
                }

                var message = await ctx.RespondAsync(embed: beforeRollEmbed);
                foreach (var emoji in emojiDiceOrder)
                    await message.CreateReactionAsync(emoji);
                await message.CreateReactionAsync(confirmEmoji);
                await message.CreateReactionAsync(cancelEmoji);
                await message.CreateReactionAsync(symbolEmoji.First().Value);

                while (true)
                {
                    // TODO: figure out how to not get ratelimited to fuck for removing the reactions
                    var reactionResult = await interact.WaitForReactionAsync(r => true, message, authMember);
                    var reaction = reactionResult.Result;
                    if (!reaction.User.Equals(ctx.Message.Author))
                        await message.DeleteReactionAsync(reaction.Emoji, reaction.User, "Cannot choose dice");
                    else
                    {
                        if (reaction.Emoji.Equals(cancelEmoji))
                        {
                            await message.DeleteAsync();
                            await ctx.RespondAsync(embed: 
                                new DiscordEmbedBuilder(embedBase).WithTitle("Roll canceled").Build());
                            return;
                        }

                        if (reaction.Emoji.Equals(confirmEmoji))
                        {
                            await message.DeleteAsync("Dice roll confirmed");
                            break;
                        }

                        if (emojiDice.ContainsKey(reaction.Emoji))
                        {
                            var type = emojiDice[reaction.Emoji];
                            toRollDice[type]++;

                            var field = beforeRollEmbed.Fields.First(em => em.Name == reaction.Emoji.ToString());
                            field.Value = toRollDice[type].ToString();

                            await message.ModifyAsync(embed: new Optional<DiscordEmbed>(beforeRollEmbed));
                            await message.DeleteReactionAsync(reaction.Emoji, reaction.User, "Reaction acknowledged");
                        }
                        else
                        {
                            await message.DeleteReactionAsync(reaction.Emoji, reaction.User, "Invalid reaction");
                        }
                    }
                }



                var rollEmbed = new DiscordEmbedBuilder(embedBase);
                {
                    string rollName = "";
                    foreach (var count in toRollDice)
                        if (count.Value > 0)
                        {
                            if (count.Value > 3)
                                rollName += emojiDice.First(k => k.Value == count.Key).Key + $"x{count.Value} ";
                            else
                                for (var i = 0; i < count.Value; i++)
                                    rollName += emojiDice.First(k => k.Value == count.Key).Key;
                        }

                    rollEmbed.WithTitle("Rolling");
                    rollEmbed.WithDescription(rollName);
                }

                var random = new Random();

                var counts = new Dictionary<Symbol, int>
                {
                    { Symbol.None, 0 },
                    { Symbol.Success, 0 },
                    { Symbol.Advantage, 0 },
                    { Symbol.Triumph, 0 },
                    { Symbol.Failure, 0 },
                    { Symbol.Threat, 0 },
                    { Symbol.Despair, 0 },
                };
                
                foreach (var count in toRollDice)
                    for (var i = 0; i < count.Value; i++)
                    {
                        var (first, second) = Dice.Roll(random, count.Key);
                        counts[first]++;
                        counts[second]++;
                    }

                var maintained = new Dictionary<Symbol, int>();
                var finalCounts = new Dictionary<Symbol, int>
                {
                    { Symbol.None, 0 },
                    { Symbol.Success, 0 },
                    { Symbol.Advantage, 0 },
                    { Symbol.Triumph, 0 },
                    { Symbol.Failure, 0 },
                    { Symbol.Threat, 0 },
                    { Symbol.Despair, 0 },
                };
                foreach (var pair in counts)
                {
                    if (pair.Key == Symbol.None) continue;

                    var symbol = pair.Key;
                    var count = pair.Value;

                    var canceledBy = symbol.GetAttribute<CanceledByAttribute>();
                    if (canceledBy.MaintainsEffects)
                        maintained.Add(symbol, count);
                    foreach (var cancel in canceledBy.Cancels)
                    {
                        count -= counts[cancel];
                        if (count < 0) count = 0;
                        if (count == 0) break;
                    }

                    finalCounts[symbol] = count;
                }

                {
                    string results = "";

                    foreach (var count in finalCounts)
                        if (count.Value > 0)
                        {
                            if (count.Value > 3)
                                results += symbolEmoji[count.Key] + $"x{count.Value} ";
                            else
                                for (var i = 0; i < count.Value; i++)
                                    results += symbolEmoji[count.Key];
                        }

                    if (string.IsNullOrWhiteSpace(results))
                        rollEmbed.AddField("", "*No results.*", inline: true);
                    else
                        rollEmbed.AddField("Roll Results", results);
                }
                {
                    string results = "";

                    foreach (var count in maintained)
                        if (count.Value > 0)
                        {
                            if (count.Value > 3)
                                results += symbolEmoji[count.Key] + $"x{count.Value} ";
                            else
                                for (var i = 0; i < count.Value; i++)
                                    results += symbolEmoji[count.Key];
                        }

                    if (!string.IsNullOrWhiteSpace(results))
                        rollEmbed.AddField("Persisted Effect", results);
                }

                await ctx.RespondAsync(embed: rollEmbed.Build());
            }
            catch (Exception e)
            {
                await ctx.ErrorWith(bot, e.Message, e.StackTrace);
            }
        }

        [Command("d6")]
        [Description("Alias for `roll 6`")]
        public async Task RollD6(CommandContext ctx,
            [Description("The number of dice to use.")] int number = 1) => await RollDice(ctx, 6, number);

        [Command("dice"), Aliases("d")]
        [Description("Rolls *n* *n*-sided dice and shows the result.")]
        public async Task RollDice(CommandContext ctx,
            // ReSharper disable once MethodOverloadWithOptionalParameter
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
