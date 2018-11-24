using DSharpBotCore.Entities;

namespace DSharpBotCore
{
    public static class Program
    {
        public static Bot Bot;
        internal static Configuration Config // static accessor for config
            => Bot.Config;
        // ~~more things than should rely on the above~~

        static void Main(string[] args) =>
            (Bot = args.Length > 0 ? new Bot(args[0]) : new Bot()).RunAsync().Wait();
    }
}
