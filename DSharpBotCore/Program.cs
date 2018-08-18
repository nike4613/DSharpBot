using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using DSharpBotCore.Modules;
using DSharpBotCore.Entities;
using System.Threading;

namespace DSharpBotCore
{
    public class Program
    {
        public static Bot Bot;
        internal static Configuration Config { get { return Bot.Config; } } // static accessor for config
        // ~~more things than should rely on the above~~

        static void Main(string[] args) => 
            (Bot = args.Length > 0 ? new Bot(args[0]) : new Bot()).RunAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }
}
