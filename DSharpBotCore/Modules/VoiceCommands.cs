using DSharpBotCore.Entities;
using DSharpBotCore.Entities.Managers;
using DSharpBotCore.Extensions;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.VoiceNext;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DSharpBotCore.Modules
{
    [Group("voice"), Description("Commands relating to voice chat.")]
    internal abstract class VoiceCommands : BaseCommandModule
    {
        private readonly Bot bot;
        private readonly Configuration config;

        protected VoiceCommands(Bot bot, Configuration config)
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

        [Command("join"), Description("Joins the user's voice channel.")]
        public async Task Join(CommandContext ctx)
        {
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
            await ctx.RespondAsync("👌");
        }

        bool stopped;

        [Command("play"), Description("Plays the song.")]
        public async Task Play(CommandContext ctx, 
            [Description("The audio to play")] string url)
        {
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

            try
            {
                const string format = "flac";

                stopped = false;

                var info = (await youtubeDl.GetUrlInfoStructs(url))[0];

                var file = Path.Combine(config.Voice.Download.DownloadLocation,
                    string.Format(YoutubeDLWrapper.YTDLInfoStruct.NameFormat, info.EntryID, info.ExtractorName) + "." + format);

                bool useLocalFile = File.Exists(file);

                ffwrap = new FFMpegWrapper(ffmpegLoc);

                if (useLocalFile)
                    ffwrap.Input = new FFMpegWrapper.FileInput(config.Voice.Download.DownloadLocation,
                        string.Format(YoutubeDLWrapper.YTDLInfoStruct.NameFormat, info.EntryID, info.ExtractorName) + "." + format);
                else
                {
                    ytdlPipe = new BufferedPipe { BlockSize = 8192 }; // literally just piping from ytdl to ffmpeg
                    ffwrap.Input = new FFMpegWrapper.PipeInput();
                    ffwrap.Outputs += new FFMpegWrapper.FileOutput(config.Voice.Download.DownloadLocation,
                        string.Format(YoutubeDLWrapper.YTDLInfoStruct.NameFormat, info.EntryID, info.ExtractorName) + "." + format,
                        format) { Options = "-ac 2 -ar 64k" };
                }

                var bpipe = new BufferedPipe { BlockSize = 3840 };
                dvStream = new DiscordVoiceStream(vnc) { BlockSize = 3840, BlockLength = 20, Volume = volume, UseEarRapeVolumeMode = earRape };
                ffwrap.Outputs += new FFMpegWrapper.PipeOutput(bpipe, "s16le") { Options = "-ac 2 -ar 48k" };
                ffwrap.Start();
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

                await ctx.ErrorWith(bot, "Error playing audio", "PlayUsing() threw an error", ($"{e.GetType().Name} in {e.TargetSite.Name}", e.Message));
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
        }

        [Command("leave"), Description("Leaves the voice channel.")]
        public async Task Leave(CommandContext ctx)
        {
            var vnext = ctx.Client.GetVoiceNext();

            var vnc = vnext.GetConnection(ctx.Guild);
            if (vnc == null)
            {
                await ctx.ErrorWith(bot, "Error disconnecting from voice", "Not currently connected");
                return;
            }

            await Stop(ctx);

            vnc.Disconnect();
            await ctx.RespondAsync("👌");
        }
    }
}
