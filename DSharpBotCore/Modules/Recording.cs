using DSharpBotCore.Entities;
using DSharpBotCore.Entities.Managers;
using DSharpBotCore.Extensions;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.VoiceNext;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using SmartFormat;

namespace DSharpBotCore.Modules
{
    [Group("recording"), Aliases("record", "rec"), Description("Commands relating to recording.")]
    public class Recording : BaseCommandModule
    {
        private Bot bot;
        private Configuration config;
        private Configuration.VoiceObject voiceConfig;
        private Configuration.VoiceObject.RecordingObject recordConfig;

        public Recording(Bot bot, Configuration config)
        {
            this.bot = bot;
            this.config = config;
            voiceConfig = config.Voice;
            recordConfig = voiceConfig.Recording;
        }

        private ConcurrentDictionary<uint, (FFMpegWrapper FFMpeg, Stream WriteStream)> recorders;

        [Command("start"), Aliases("s", "on"), Description("Starts recording.")]
        public async Task StartRecording(CommandContext ctx)
        {
            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            var voiceNext = ctx.Client.GetVoiceNext();

            var vnc = voiceNext.GetConnection(ctx.Guild);
            if (vnc == null)
            {
                await ctx.ErrorWith(bot, "Could not start recording", "Not in voice channel");
                return;
            }

            if (recorders != null)
            {
                await ctx.ErrorWith(bot, "Could not start recording", "Already recording");
                return;
            }

            recorders = new ConcurrentDictionary<uint, (FFMpegWrapper FFMpeg, Stream WriteStream)>();
            vnc.VoiceReceived += OnVoiceRecieved;

            //var b = voiceNext.IsIncomingEnabled;

            await ctx.Client.UpdateStatusAsync(new DiscordActivity("you", ActivityType.ListeningTo), UserStatus.DoNotDisturb);
            await ctx.RespondAsync("Started recording.");
        }

        [Command("stop"), Aliases("off"), Description("Starts recording.")]
        public async Task StopRecording(CommandContext ctx)
        {
            if (config.Commands.MiscDeleteTrigger)
                await ctx.Message.DeleteAsync("Delete trigger.");

            var voiceNext = ctx.Client.GetVoiceNext();

            var vnc = voiceNext.GetConnection(ctx.Guild);
            if (vnc == null)
            {
                await ctx.ErrorWith(bot, "Could not stop recording", "Not in voice channel");
                return;
            }

            if (recorders == null)
            {
                await ctx.ErrorWith(bot, "Could not stop recording", "Not recording");
                return;
            }

            var msg = await ctx.RespondAsync("Stopping...");
            
            vnc.VoiceReceived -= OnVoiceRecieved;
            foreach (var record in recorders)
            {
                record.Value.WriteStream.Close();
                await record.Value.FFMpeg.AwaitProcessEnd;
                //await record.Value.FFMpeg.Stop();
            }
            recorders = null;
            await ctx.Client.UpdateStatusAsync(userStatus: UserStatus.Online);

            await msg.ModifyAsync("Stopped recording.");

            recordIndex++;
        }

        private int recordIndex;
        
        private async Task OnVoiceRecieved(VoiceReceiveEventArgs e)
        {
            Stream streamOut;
            if (!recorders.TryGetValue(e.SSRC, out var value))
            {
                var filename =
                    Smart.Format(recordConfig.NameFormat, new {Member = e.User, Index = recordIndex, Date = DateTime.Now, e.SSRC}) +
                    "." + recordConfig.DownloadFormat;

                var ff = new FFMpegWrapper(voiceConfig.FFMpegLocation);
                var fstreamOut = new FFMpegWrapper.StreamInput()
                {
                    Options = "-c:a 2 -f s16le -r:a 48000"
                };
                ff.Input = fstreamOut;
                ff.Outputs.Add(new FFMpegWrapper.FileOutput(recordConfig.DownloadLocation, filename,
                                                            recordConfig.DownloadFormat) {NormalizeVolume = false});
                ff.Start();

                recorders.TryAdd(e.SSRC, (ff, streamOut = fstreamOut));
            }
            else
                streamOut = value.WriteStream;

            var buf = e.Voice.ToArray();
            await streamOut.WriteAsync(buf, 0, buf.Length);
            await streamOut.FlushAsync();
        }
    }
}
