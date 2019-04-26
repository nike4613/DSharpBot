using DSharpBotCore.Entities;
using DSharpBotCore.Entities.Managers;
using DSharpBotCore.Extensions;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.VoiceNext;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;

namespace DSharpBotCore.Modules
{
    [Group("voice"), Description("Commands relating to voice chat.")]
    // ReSharper disable once ClassNeverInstantiated.Global
    internal class VoiceCommands : BaseCommandModule
    {
        private readonly Bot bot;
        private readonly Configuration config;

        public VoiceCommands(Bot bot, Configuration config, PlayQueue queue, YoutubeDLWrapper ydl)
        {
            this.bot = bot;
            this.config = config;
            this.queue = queue;
            ytdl = ydl;
        }

        private PlayQueue queue;
        private YoutubeDLWrapper ytdl;

        bool earRape;

        private async Task RespondTemporary(Task<DiscordMessage> messageTask, bool waitFor = true)
        {
            var message = await messageTask;
            if (config.Voice.ConfirmationMessageLength != TimeSpan.Zero)
            {
                var task = Task.Run(async () => {
                    await Task.Delay(config.Voice.ConfirmationMessageLength);
                    await message.DeleteAsync("Configured to delete.");
                });
                if (waitFor) await task;
            }
        }

        [Command("join"), Description("Joins the user's voice channel.")]
        public async Task Join(CommandContext ctx)
        {
            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            var vnext = ctx.Client.GetVoiceNext();

            var vnc = vnext.GetConnection(ctx.Guild);
            if (vnc != null)
            {
                await ctx.ErrorWith(bot, "Error connecting to voice", "Already connected to channel", ("Current channel", vnc.Channel.Mention));
                return;
            }

            var chn = ctx.Member?.VoiceState?.Channel;
            if (chn == null)
            {
                await ctx.ErrorWith(bot, "Error connecting to voice", "User not in a channel", ("User", ctx.Member?.Mention));
                return;
            }

            vnc = await vnext.ConnectAsync(chn);

            queue.StartPlayer(new DiscordVoiceStream(vnc)
                                  { BlockSize = 3840, BlockLength = 20, UseEarRapeVolumeMode = earRape });
            await RespondTemporary(ctx.RespondAsync("👌"));
        }

        [Command("play"), Description("Plays the song.")]
        public async Task Play(CommandContext ctx, 
            [Description("The audio to play")] string url)
        {
            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            var embed = new DiscordEmbedBuilder();

            embed.WithTitle("*Looking up info...*")
                 .WithUserAsAuthor(ctx.User)
                 .WithColor(new DiscordColor("#352fe0"))
                 .WithDefaultFooter(bot);

            var lookupMessage = await ctx.RespondAsync(embed: embed);

            var entries = await ytdl.GetUrlInfoStructs(new Uri(url));

            if (entries.Length == 0)
            {
                embed.WithTitle("Error in lookup")
                     .WithColor(new DiscordColor("#ff0000"))
                     .WithDescription("No entries found for URL");
                var msg = lookupMessage.ModifyAsync(embed: embed.Build());
                await RespondTemporary(msg);
                return;
            }
            if (entries.Length == 1)
            {
                embed.WithTitle($"Added **{entries[0].Title.Sanitize()}**")
                     .WithImageUrl(entries[0].Thumbnail)
                     .WithDescription($"**[{entries[0].Title.Sanitize()}]({entries[0].Url})**\n[{entries[0].Author.Sanitize()}]({entries[0].AuthorUri})");
            }
            else
            {
                embed.WithTitle($"Added **{entries.Length}** items to the queue")
                     .WithUrl(url);
                     //.WithDescription($"The playlist has **{entries.Length}** entries.");
            }

            var channel = ctx.Channel;
            foreach (var entry in entries)
            {
                queue.Add(new PlayQueue.QueueEntry(entry)
                {
                    OnPlayStart = () =>
                    {
                        var builder = new DiscordEmbedBuilder();
                        builder.WithTitle("Now Playing")
                              .WithImageUrl(entry.Thumbnail)
                              .WithDescription($"**[{entry.Title.Sanitize()}]({entry.Url})**\n[{entry.Author.Sanitize()}]({entry.AuthorUri})")
                              .WithColor(new DiscordColor("#352fe0"))
                              .WithDefaultFooter(bot);
                        channel.SendMessageAsync(embed: builder);
                    },
                    OnPlayError = e =>
                    {
                        var builder = new DiscordEmbedBuilder();
                        builder.WithTitle("Error while playing song")
                               .WithImageUrl(entry.Thumbnail)
                               .WithDescription($"Song:\n\t**[{entry.Title.Sanitize()}]({entry.Url})**\n\t[{entry.Author.Sanitize()}]({entry.AuthorUri})")
                               .WithColor(new DiscordColor("#ff0000"))
                               .WithDefaultFooter(bot);
                        builder.AddField($"{e.GetType().FullName}: {e.Message}", e.StackTrace);
                        channel.SendMessageAsync(embed: builder);
                    }
                });
            }

            var message = lookupMessage.ModifyAsync(embed: embed.Build());

            if (config.Voice.IsNowPlayingConfirmation)
                await RespondTemporary(message, false);
            else
                await message;
        }

        [Command("volume"), Description("Gets or sets the volume"), Priority(0)]
        public async Task GetVolume(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            await ctx.RespondAsync($"The current volume is **{queue.Volume:p}**");
        }

        [Command("volume"), Description("Gets or sets the volume"), Priority(1)]
        public async Task SetVolume(CommandContext ctx, [Description("The new volume out of 100")] double newvol)
        {
            await ctx.TriggerTypingAsync();

            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            if (newvol < 0 || newvol > 250)
            {
                await ctx.ErrorWith(bot, "Volume out of range", "Number must be between 0 and 250!");
                return;
            }

            queue.Volume = newvol / 100f;

            await ctx.RespondAsync($"The current volume is now **{queue.Volume:p}**");
        }

        [Command("earrape"), Description("Gets or sets ear rape mode"), Priority(0)]
        public async Task GetEarRapeMode(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            await ctx.RespondAsync($"Ear rape mode is currently **{(earRape ? "on" : "off")}**");
        }

        [Command("earrape"), Description("Gets or sets ear rape mode"), Priority(1)]
        public async Task SetEarRape(CommandContext ctx, [Description("The new state, as in 'on' or 'off'")] string sNewState)
        {
            await SetEarRape(ctx, sNewState.ToLower() == "on");
        }

        [Command("earrape"), Description("Gets or sets ear rape mode"), Priority(2)]
        public async Task SetEarRape(CommandContext ctx, [Description("The new state")] bool newState)
        {
            await ctx.TriggerTypingAsync();

            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            string newStateS = newState ? "on" : "off";

            earRape = newState;
            if (queue.VoiceStream != null) queue.VoiceStream.UseEarRapeVolumeMode = earRape;

            await ctx.RespondAsync($"Ear rape mode is now **{newStateS}**.");
        }

        [Command("loop"), Description("Gets or sets loop mode"), Priority(0)]
        public async Task GetLoopMode(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            await ctx.RespondAsync($"Looping is currently **{(queue.Loop ? "on" : "off")}**");
        }

        [Command("loop"), Description("Gets or sets loop mode"), Priority(1)]
        public async Task SetLoopMode(CommandContext ctx, [Description("The new state, as in 'on' or 'off'")] string sNewState)
        {
            await SetLoopMode(ctx, sNewState.ToLower() == "on");
        }

        [Command("loop"), Description("Gets or sets loop mode"), Priority(2)]
        public async Task SetLoopMode(CommandContext ctx, [Description("The new state")] bool newState)
        {
            await ctx.TriggerTypingAsync();

            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            string newStateS = newState ? "on" : "off";

            queue.Loop = newState;

            await ctx.RespondAsync($"Looping is now **{newStateS}**.");
        }

        [Command("next"), Description("Continues to the next song."), Priority(2)]
        public async Task Next(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            try
            {
                queue.Next();
                await RespondTemporary(ctx.RespondAsync("Continuing to next."));
            }
            catch (Exception e)
            {
                await ctx.ErrorWith(bot, "Error continuing to next", "Next() threw an error", ($"{e.GetType().Name} in {e.TargetSite.Name}", e.Message));
            }
        }
        
        [Command("clear"), Description("Clears the queue."), Priority(2)]
        public async Task Clear(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            try
            {
                queue.Clear();
                await RespondTemporary(ctx.RespondAsync("Queue cleared."));
            }
            catch (Exception e)
            {
                await ctx.ErrorWith(bot, "Error clearing", "Clear() threw an error", ($"{e.GetType().Name} in {e.TargetSite.Name}", e.Message));
            }
        }

        [Command("pause"), Description("Pauses the current song."), Priority(2)]
        public async Task Pause(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            try
            {
                queue.PausePlaying();
                await RespondTemporary(ctx.RespondAsync("Paused."));
            }
            catch (Exception e)
            {
                await ctx.ErrorWith(bot, "Error pausing", "Pause() threw an error", ($"{e.GetType().Name} in {e.TargetSite.Name}", e.Message));
            }
        }

        [Command("resume"), Aliases("res"), Description("Pauses the current song."), Priority(2)]
        public async Task Resume(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            try
            {
                queue.ResumePlaying();
                await RespondTemporary(ctx.RespondAsync("Resumed."));
            }
            catch (Exception e)
            {
                await ctx.ErrorWith(bot, "Error resuming", "Resume() threw an error", ($"{e.GetType().Name} in {e.TargetSite.Name}", e.Message));
            }
        }

        [Command("pausequeue"), Aliases("pauseq"), Description("Pauses the current song."), Priority(2)]
        public async Task PauseQueue(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            try
            {
                queue.PauseQueue();
                await RespondTemporary(ctx.RespondAsync("Paused queue."));
            }
            catch (Exception e)
            {
                await ctx.ErrorWith(bot, "Error pausing", "PauseQueue() threw an error", ($"{e.GetType().Name} in {e.TargetSite.Name}", e.Message));
            }
        }

        [Command("resumequeue"), Aliases("resumeq","resq"), Description("Pauses the current song."), Priority(2)]
        public async Task ResumeQueue(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            try
            {
                queue.ResumeQueue();
                await RespondTemporary(ctx.RespondAsync("Resumed queue."));
            }
            catch (Exception e)
            {
                await ctx.ErrorWith(bot, "Error resuming", "ResumeQueue() threw an error", ($"{e.GetType().Name} in {e.TargetSite.Name}", e.Message));
            }
        }

        [Command("stop"), Description("Stops the music.")]
        public async Task Stop(CommandContext ctx)
        {
            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            var vnext = ctx.Client.GetVoiceNext();

            var vnc = vnext.GetConnection(ctx.Guild);
            if (vnc == null)
            {
                await ctx.ErrorWith(bot, "Error stopping audio", "Not currently connected");
                return;
            }

            try
            { // todo
                queue.PauseQueue();
                queue.Next();
            }
            catch (Exception e)
            {
                await ctx.ErrorWith(bot, "Error stopping audio", "Stop() threw an error", ($"{e.GetType().Name} in {e.TargetSite.Name}", e.Message));
            }

            await RespondTemporary(ctx.RespondAsync("Audio stopped."));
        }

        [Command("leave"), Description("Leaves the voice channel.")]
        public async Task Leave(CommandContext ctx)
        {
            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            var vnext = ctx.Client.GetVoiceNext();

            var vnc = vnext.GetConnection(ctx.Guild);
            if (vnc == null)
            {
                await ctx.ErrorWith(bot, "Error disconnecting from voice", "Not currently connected");
                return;
            }

            queue.StopPlayer();

            vnc.Disconnect();
            await RespondTemporary(ctx.RespondAsync("👌"));
        }

        [Command("queue"), Description("Shows (part of) the current queue.")]
        public async Task GetQueue(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            var interact = ctx.Client.GetInteractivity();

            try
            {
                DiscordEmbedBuilder embed = null;//new DiscordEmbedBuilder();
                
                var pageSize = config.Voice.QueuePageSize;

                var pages = new List<Page>();

                var paused = queue.PauseQueue();

                var queueLen = queue.Count;
                var pageCount = (queueLen-1) / pageSize + 1;

                if (queueLen != 0)
                {
                    int i = 0;
                    foreach (var qi in queue)
                    {
                        if (embed == null)
                        {
                            var endRange = (pages.Count + 1) * pageSize;
                            if (queueLen < endRange) endRange = queueLen;
                            embed = new DiscordEmbedBuilder()
                                   .WithTitle($"Page **{pages.Count + 1}**/**{pageCount}**")
                                   .WithDescription($"The queue currently has **{queueLen}** items, showing **{pages.Count * pageSize + 1}**-**{endRange}**")
                                    //.WithDescription($"Here are the first {count}")
                                   .WithMemberAsAuthor(ctx.Member)
                                   .WithColor(new DiscordColor("#352fe0"))
                                   .WithDefaultFooter(bot);
                        }

                        embed.AddField($"**{++i}.** *{qi.Title}*",
                                       $"__[link]({qi.Link})__ by *[{qi.Artist}]({qi.ArtistLink})*");

                        if (i >= (pages.Count + 1) * pageSize)
                        {
                            pages.Add(new Page
                            {
                                Embed = embed
                            });
                            embed = null;
                        }
                    }

                    if (embed != null)
                        pages.Add(new Page
                        {
                            Embed = embed
                        });

                    if (paused)
                        queue.ResumeQueue();

                    await interact.SendPaginatedMessage(ctx.Channel, ctx.User, pages);
                }
                else
                {
                    embed = new DiscordEmbedBuilder()
                           .WithTitle("Page **1**/**1**")
                           .WithDescription($"*The queue is empty*")
                           .WithMemberAsAuthor(ctx.Member)
                           .WithColor(new DiscordColor("#352fe0"))
                           .WithDefaultFooter(bot);
                    await ctx.RespondAsync(embed: embed);
                }
            }
            catch (Exception e)
            {
                await ctx.ErrorWith(bot, "Error viewing queue", null, ($"{e.GetType().Name} in {e.TargetSite.Name}", e.Message), ("", e.StackTrace));
            }
        }
    }
}
