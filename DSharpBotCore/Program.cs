using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DSharpBotCore
{
    public class Program
    {
        public static DiscordClient Client;
        public static Configuration Config;
        public static CommandsNextModule commands;
        public static InteractivityModule interactivity;

        static void Main(string[] args) => MainAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();

        static async Task MainAsync(string[] args)
        {
            Config = LoadConfiguration("config.json");

            if (Config.Token == null)
                Console.Error.WriteLine("Key 'token' in config file must be not null!");

            Client = new DiscordClient(new DiscordConfiguration
            {
                Token = Config.Token,
                TokenType = TokenType.Bot,
                UseInternalLogHandler = Config.Log,
                LogLevel = Config.LogLevel
            });

            commands = Client.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefix = Config.Prefix
            });
            commands.RegisterCommands<Commands>();
            interactivity = Client.UseInteractivity(new InteractivityConfiguration
            {
                PaginationBehaviour = Config.Interactivity.Pagination.Behaviour,
                PaginationTimeout = Config.Interactivity.Pagination.Timeout,
                Timeout = Config.Interactivity.Timeout
            });

            Client.MessageCreated += async e =>
            {
                if (e.Message.Content.ToLower().StartsWith("ping"))
                    await e.Message.RespondAsync("pong!");
            };
            
            Client.ClientErrored += Client_ClientError;

            await Client.ConnectAsync();
            await Task.Delay(-1);
        }

        private static Task Client_ClientError(ClientErrorEventArgs e)
        {
            // let's log the details of the error that just 
            // occured in our client
            e.Client.DebugLogger.LogMessage(LogLevel.Error, "ExampleBot", $"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}", DateTime.Now);

            // since this method is not async, let's return
            // a completed task, so that no additional work
            // is done
            return Task.CompletedTask;
        }

        static Configuration LoadConfiguration(string filename)
        {
            Configuration config;

            if (File.Exists(filename))
            {
                config = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(filename));
            }
            else
            {
                config = new Configuration
                {
                    Token = null,
                    Prefix = "!"
                };
                File.WriteAllText(filename, JsonConvert.SerializeObject(config));
            }

            return config;
        }
    }
}
