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

        FFController ffstream = new FFController(FFController.FFLogLevel.quiet);

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

        [Command("play"), Description("Leaves the voice channel.")]
        public async Task Play(CommandContext ctx)
        {
            var vnext = ctx.Client.GetVoiceNext();

            var vnc = vnext.GetConnection(ctx.Guild);
            if (vnc == null)
            {
                await ctx.ErrorWith(bot, "Error playing audio", "Not currently connected");
                return;
            }



            await vnc.SendSpeakingAsync(true); // send a speaking indicator

            try
            {
                await ffstream.PlayUsingAsync("Z:/Users/aaron/Desktop/music/Creo/Creo - Start Your Engines.flac", vnc.SendAsync);
            }
            catch (Exception e)
            {
                await ctx.ErrorWith(bot, "Error stopping audio", "PlayUsing() threw an error", ($"{e.GetType().Name} in {e.TargetSite.Name}", e.Message));
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
                ffstream.Stop();
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

            if (ffstream.IsPlaying)
                ffstream.Stop();

            vnc.Disconnect();
            await ctx.RespondAsync("👌");
        }
    }
}
