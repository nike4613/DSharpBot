using DSharpBotCore.Entities;
using DSharpBotCore.Entities.Managers;
using DSharpBotCore.Extensions;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.VoiceNext;
using System;
using System.IO;
using System.Threading.Tasks;
using DSharpPlus.Entities;

namespace DSharpBotCore.Modules
{
    [Group("voice"), Description("Commands relating to voice chat.")]
    // ReSharper disable once ClassNeverInstantiated.Global
    internal class VoiceCommands : BaseCommandModule
    {
        private readonly Bot bot;
        private readonly Configuration config;

        public VoiceCommands(Bot bot, Configuration config)
        {
            this.bot = bot;
            this.config = config;
            youtubeDl = new YoutubeDLWrapper(config.Voice.Download.YoutubeDlLocation);
            ffmpegLoc = config.Voice.FFMpegLocation;
            volume = config.Voice.DefaultVolume;
        }

        private readonly YoutubeDLWrapper youtubeDl;
        private readonly string ffmpegLoc;
        private FFMpegWrapper ffwrap;
        private BufferedPipe ytdlPipe;
        private DiscordVoiceStream dvStream;

        bool earRape;
        double volume;

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

            await vnext.ConnectAsync(chn);
            await RespondTemporary(ctx.RespondAsync("👌"));
        }

        bool stopped;

        [Command("play"), Description("Plays the song.")]
        public async Task Play(CommandContext ctx, 
            [Description("The audio to play")] string url)
        {
            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            var vnext = ctx.Client.GetVoiceNext();

            var vnc = vnext.GetConnection(ctx.Guild);
            if (vnc == null)
            {
                await ctx.ErrorWith(bot, "Error playing audio", "Not currently connected");
                return;
            }

            await vnc.SendSpeakingAsync(); // send a speaking indicator

            if (ffwrap != null)
            {
                await ctx.ErrorWith(bot, "Error playing audio", "Cannot play 2 sounds at a time");
                return;
            }

            DiscordMessage lookupMessage = null;

            try
            {
                const string format = "flac";

                stopped = false;

                var embed = new DiscordEmbedBuilder();

                embed.WithTitle("*Looking up info...*")
                     .WithUserAsAuthor(ctx.User)
                     .WithColor(new DiscordColor("#352fe0"))
                     .WithDefaultFooter(bot);

                lookupMessage = await ctx.RespondAsync(embed: embed);

                var info = (await youtubeDl.GetUrlInfoStructs(new Uri(url)))[0];

                var file = Path.Combine(config.Voice.Download.DownloadLocation,
                                        string.Format(YoutubeDLWrapper.YTDLInfoStruct.NameFormat, info.EntryID,
                                                      info.ExtractorName) + "." + format);

                bool useLocalFile = File.Exists(file);

                ffwrap = new FFMpegWrapper(ffmpegLoc);

                if (useLocalFile)
                    ffwrap.Input = new FFMpegWrapper.FileInput(config.Voice.Download.DownloadLocation,
                                                               string.Format(YoutubeDLWrapper.YTDLInfoStruct.NameFormat,
                                                                             info.EntryID, info.ExtractorName) + "." +
                                                               format);
                else
                {
                    ytdlPipe = new BufferedPipe {BlockSize = 8192}; // literally just piping from ytdl to ffmpeg
                    ffwrap.Input = new FFMpegWrapper.PipeInput(ytdlPipe);
                    ffwrap.Outputs += new FFMpegWrapper.FileOutput(config.Voice.Download.DownloadLocation,
                                                                   string
                                                                      .Format(YoutubeDLWrapper.YTDLInfoStruct.NameFormat,
                                                                              info.EntryID, info.ExtractorName) + "." +
                                                                   format,
                                                                   format) {Options = "-ac 2 -ar 64k"};
                }

                var bpipe = new BufferedPipe {BlockSize = 3840};
                dvStream = new DiscordVoiceStream(vnc)
                    {BlockSize = 3840, BlockLength = 20, Volume = volume, UseEarRapeVolumeMode = earRape};
                bpipe.Outputs += dvStream;
                ffwrap.Outputs += new FFMpegWrapper.PipeOutput(bpipe, "s16le") {Options = "-ac 2 -ar 48k"};
                ffwrap.Start();

                embed.WithTitle("Now Playing")
                     .WithDescription($"**[{info.Title.Sanitize()}]({info.Url})**\n[{info.Author.Sanitize()}]({info.AuthorUri})")
                     .WithImageUrl(info.Thumbnail)
                     .WithDefaultFooter(bot);

                var message = lookupMessage.ModifyAsync(embed: embed.Build());

                if (config.Voice.IsNowPlayingConfirmation)
                    await RespondTemporary(message, false);
                else
                    await message;

                if (!useLocalFile) await youtubeDl.StreamInItem(info, ytdlPipe);
                await bpipe.AwaitEndOfStream;
                await ffwrap.AwaitProcessEnd;

                ytdlPipe = null;
                ffwrap = null;
                dvStream = null;

                if (stopped)
                    File.Delete(file);
            }
            catch (Exception e)
            {
                ytdlPipe = null;
                ffwrap = null;

                lookupMessage?.DeleteAsync("Should never stick around.")?.Wait();
                await ctx.ErrorWith(bot, "Error playing audio", "PlayUsing() threw an error",
                                    ($"{e.GetType().Name} in {e.TargetSite.Name}", e.Message));
                return;
            }

            await vnc.SendSpeakingAsync(false); // we're not speaking anymore
        }

        [Command("volume"), Description("Gets or sets the volume"), Priority(0)]
        public async Task GetVolume(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            if (config.Commands.Roll.DeleteTrigger)
                await ctx.Message.DeleteAsync();

            await ctx.RespondAsync($"The current volume is **{volume:p}**");
        }

        [Command("volume"), Description("Gets or sets the volume"), Priority(1)]
        public async Task SetVolume(CommandContext ctx, [Description("The new volume out of 100")] double newvol)
        {
            await ctx.TriggerTypingAsync();

            if (config.Commands.Roll.DeleteTrigger)
                await ctx.Message.DeleteAsync();

            if (newvol < 0 || newvol > 100)
            {
                await ctx.ErrorWith(bot, "Volume out of range", "Number must be between 0 and 100!");
                return;
            }

            volume = newvol / 100f;
            if (dvStream != null) dvStream.Volume = volume;

            await ctx.RespondAsync($"The current volume is now **{volume:p}**");
        }

        [Command("earrape"), Description("Gets or sets ear rape mode"), Priority(0)]
        public async Task GetEarRapeMode(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            if (config.Commands.Roll.DeleteTrigger)
                await ctx.Message.DeleteAsync();

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

            if (config.Commands.Roll.DeleteTrigger)
                await ctx.Message.DeleteAsync();
            
            string newStateS = newState ? "on" : "off";

            earRape = newState;
            if (dvStream != null) dvStream.UseEarRapeVolumeMode = earRape;

            await ctx.RespondAsync($"Ear rape mode is now **{newStateS}**.");
        }

        [Command("stop"), Description("Leaves the voice channel.")]
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
            {
                stopped = true;
                if (ytdlPipe != null) ytdlPipe.Close();
                if (ffwrap != null) await ffwrap.Stop();
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

            await Stop(ctx);

            vnc.Disconnect();
            await RespondTemporary(ctx.RespondAsync("👌"));
        }
    }
}
