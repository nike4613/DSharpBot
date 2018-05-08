using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DSharpBotCore.Extensions
{
    public static class DiscordEmbedBuilderExtensions
    {
        public static DiscordEmbedBuilder WithDefaultFooter(this DiscordEmbedBuilder builder)
        {
            return builder
                .WithTimestamp(DateTime.Now)
                .WithFooter(
                    text: "Icons Bot",
                    icon_url: Program.Client.CurrentUser.AvatarUrl
                );
        }

        public static DiscordEmbedBuilder WithUserAsAuthor(this DiscordEmbedBuilder builder, DiscordUser author)
        {
            return builder.WithAuthor(
                    name: author.Username,
                    icon_url: author.AvatarUrl
                );
        }
    }
}
