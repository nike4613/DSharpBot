using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DSharpBotCore.Extensions
{
    public static class DiscordEmbedBuilderExtensions
    {
        public static DiscordEmbedBuilder WithDefaultFooter(this DiscordEmbedBuilder builder, Bot bot)
        {
            return builder
                .WithTimestamp(DateTime.Now)
                .WithFooter(
                    text: bot.Config.Name,
                    iconUrl: bot.Client.CurrentUser.AvatarUrl
                );
        }

        public static DiscordEmbedBuilder WithUserAsAuthor(this DiscordEmbedBuilder builder, DiscordUser author)
        {
            return builder.WithAuthor(
                    name: author.Username,
                    iconUrl: author.AvatarUrl
                );
        }

        public static DiscordEmbedBuilder WithMemberAsAuthor(this DiscordEmbedBuilder builder, DiscordMember author)
        {
            return builder.WithAuthor(
                    name: author.Nickname,
                    iconUrl: author.AvatarUrl
                );
        }
    }
}
