using DSharpBotCore.Entities;
using DSharpBotCore.Entities.Managers;
using DSharpBotCore.Extensions;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.VoiceNext;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSharpBotCore.Modules
{
    [Group("voice"), Description("Commands relating to voice chat.")]
    class VoiceCommands : BaseCommandModule
    {
        Bot bot;
        Configuration config;
        DownloadManager manager;
        public VoiceCommands(Bot bot, Configuration config, DownloadManager manager)
        {
            this.bot = bot;
            this.config = config;
            this.manager = manager;
            youtubedl = new YoutubeDLWrapper(config.Voice.Download.YoutubeDlLocation);
            ffmpegLoc = config.Voice.FFMpegLocation;
        }

        YoutubeDLWrapper youtubedl;
        string ffmpegLoc;
        FFMpegWrapper ffwrap;
        BufferedPipe ytdlPipe;

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
                await ctx.ErrorWith(bot, "Error connecting to voice", "User not in a channel", ("User", ctx.Member.Mention));
                return;
            }

            vnc = await vnext.ConnectAsync(chn);
            await ctx.RespondAsync("👌");
        }

        bool stopped = false;

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

            await vnc.SendSpeakingAsync(true); // send a speaking indicator

            if (ffwrap != null)
            {
                await ctx.ErrorWith(bot, "Error playing audio", "Cannot play 2 sounds at a time");
                return;
            }

            try
            {
                const string format = "flac";

                stopped = false;

                var info = (await youtubedl.GetUrlInfoStructs(url))[0];

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
                    ffwrap.Input = new FFMpegWrapper.PipeInput(ytdlPipe);
                    ffwrap.Outputs += new FFMpegWrapper.FileOutput(config.Voice.Download.DownloadLocation,
                        string.Format(YoutubeDLWrapper.YTDLInfoStruct.NameFormat, info.EntryID, info.ExtractorName) + "." + format,
                        format) { Options = "-ac 2 -ar 64k" };
                }

                var bpipe = new BufferedPipe { BlockSize = 3840 };
                bpipe.Outputs += new DiscordVoiceStream(vnc) { BlockSize = 3840, BlockLength = 20};
                ffwrap.Outputs += new FFMpegWrapper.PipeOutput(bpipe, "s16le") { Options = "-ac 2 -ar 48k" };
                ffwrap.Start();
                if (!useLocalFile) await youtubedl.StreamInItem(info, ytdlPipe);
                await bpipe.AwaitEndOfStream;
                await ffwrap.AwaitProcessEnd;

                ytdlPipe = null;
                ffwrap = null;

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
                return;
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
