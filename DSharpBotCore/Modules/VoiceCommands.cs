using DSharpBotCore.Entities;
using DSharpBotCore.Extensions;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.VoiceNext;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace DSharpBotCore.Modules
{
    [Group("voice"), Description("Commands relating to voice chat.")]
    class VoiceCommands : BaseCommandModule
    {
        Bot bot;
        Configuration config;
        public VoiceCommands(Bot bot, Configuration config)
        {
            this.bot = bot;
            this.config = config;
        }

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

            await vnc.SendSpeakingAsync(true); // send a speaking indicator

            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $@"-i ""Z:/Users/aaron/Desktop/music/Creo/Creo - Start Your Engines.flac"" -ac 2 -f s16le -ar 48000 pipe:1",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            var ffmpeg = Process.Start(psi);
            var ffout = ffmpeg.StandardOutput.BaseStream;

            var buff = new byte[3840];
            var br = 0;
            while ((br = ffout.Read(buff, 0, buff.Length)) > 0)
            {
                if (br < buff.Length) // not a full sample, mute the rest
                    for (var i = br; i < buff.Length; i++)
                        buff[i] = 0;

                await vnc.SendAsync(buff, 20);
            }

            await vnc.SendSpeakingAsync(false); // we're not speaking anymore
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

            vnc.Disconnect();
            await ctx.RespondAsync("👌");
        }
    }
}
