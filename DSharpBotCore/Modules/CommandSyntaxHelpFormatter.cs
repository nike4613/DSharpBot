using DSharpBotCore.Entities;
using DSharpBotCore.Extensions;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Converters;
using DSharpPlus.CommandsNext.Entities;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DSharpBotCore.Modules
{
    /// <summary>
    /// DO NOT USE
    /// </summary>
    class CommandSyntaxHelpFormatter : DefaultHelpFormatter
    {
        private DiscordEmbedBuilder embed;
        private CommandArgument[] arguments;
        private string name;
        private string desc;
        private bool isGroupExec;
        private Command[] subcommands;
        private string[] aliases;

        private CommandsNextExtension cext;

        public CommandSyntaxHelpFormatter(CommandsNextExtension cnext) : base(cnext)
        {
        }
    }
}
