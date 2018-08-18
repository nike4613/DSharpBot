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
using DSharpPlus.VoiceNext;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace DSharpBotCore
{
    public class Bot : IDisposable
    {
        public DiscordClient Client;
        public Configuration Config;
        public CommandsNextExtension Commands;
        public InteractivityExtension Interactivity;
        public VoiceNextExtension Voice;
        public CancellationTokenSource CTS;

        internal void WriteCenter(string value, int skipline = 0)
        {
            for (int i = 0; i < skipline; i++)
                Console.WriteLine();

            Console.SetCursorPosition((Console.WindowWidth - value.Length) / 2, Console.CursorTop);
            Console.WriteLine(value);
        }

        public Bot(string confingFile = "config.json")
        {
            try
            {
                Config = LoadConfiguration(confingFile);

                if (Config.Token == null)
                    throw new ArgumentException("Bot token is null!");
            }
            catch (Exception e)
            {
                const int width = 120, height = 24;

                #region Pretty error!
                Console.SetWindowPosition(0, 0);
                Console.SetWindowSize(width, height);
                Console.SetBufferSize(width, height);
                Console.SetCursorPosition(1, 1);
                Console.ForegroundColor = ConsoleColor.Red;
                WriteCenter(e.Message);
                Console.BackgroundColor = ConsoleColor.Black;
                Console.ForegroundColor = ConsoleColor.Black;
                Console.SetCursorPosition(1, height - 2); // last 2 lines
                Console.ForegroundColor = ConsoleColor.Yellow;
                WriteCenter("Uh-oh! Looks like something went horribly wrong when reading the config file!");
                WriteCenter("Please fix it and try again.");
                Console.BackgroundColor = ConsoleColor.Black;
                Console.ForegroundColor = ConsoleColor.Black;

                Console.SetCursorPosition(1, height - 17);
                Console.BackgroundColor = ConsoleColor.Red;
                Console.ForegroundColor = ConsoleColor.Black;
                WriteCenter("▒▒▒▒▒▒▒▒▒▄▄▄▄▒▒▒▒▒▒▒");
                WriteCenter("▒▒▒▒▒▒▄▀▀▓▓▓▀█▒▒▒▒▒▒");
                WriteCenter("▒▒▒▒▄▀▓▓▄██████▄▒▒▒▒");
                WriteCenter("▒▒▒▄█▄█▀░░▄░▄░█▀▒▒▒▒");
                WriteCenter("▒▒▄▀░██▄░░▀░▀░▀▄▒▒▒▒");
                WriteCenter("▒▒▀▄░░▀░▄█▄▄░░▄█▄▒▒▒");
                WriteCenter("▒▒▒▒▀█▄▄░░▀▀▀█▀▒▒▒▒▒");
                WriteCenter("▒▒▒▄▀▓▓▓▀██▀▀█▄▀▀▄▒▒");
                WriteCenter("▒▒█▓▓▄▀▀▀▄█▄▓▓▀█░█▒▒");
                WriteCenter("▒▒▀▄█░░░░░█▀▀▄▄▀█▒▒▒");
                WriteCenter("▒▒▒▄▀▀▄▄▄██▄▄█▀▓▓█▒▒");
                WriteCenter("▒▒█▀▓█████████▓▓▓█▒▒");
                WriteCenter("▒▒█▓▓██▀▀▀▒▒▒▀▄▄█▀▒▒");
                WriteCenter("▒▒▒▀▀▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒");

                Console.BackgroundColor = ConsoleColor.Black;
                Console.ForegroundColor = ConsoleColor.Black;
                Console.SetCursorPosition(1, 1);
                Console.ReadKey();
                Environment.Exit(1);
                #endregion
            }

            Environment.SetEnvironmentVariable("PATH", Environment.GetEnvironmentVariable("PATH") + ";" + Config.LibraryPath);

            Client = new DiscordClient(new DiscordConfiguration
            {
                Token = Config.Token,
                TokenType = TokenType.Bot,
                UseInternalLogHandler = Config.Log,
                LogLevel = Config.LogLevel,
                AutoReconnect = Config.Connection.AutoReconnect,
            });

            Interactivity = Client.UseInteractivity(new InteractivityConfiguration
            {
                PaginationBehavior = Config.Interactivity.Pagination.Behaviour,
                PaginationTimeout = Config.Interactivity.Pagination.Timeout,
                Timeout = Config.Interactivity.Timeout
            });

            // Don't use voice if not enabled
            if (Config.Voice.Enabled) Voice = Client.UseVoiceNext();

            CTS = new CancellationTokenSource();

            var services = new ServiceCollection();

            services.AddSingleton(Config)
                    .AddSingleton(CTS)
                    .AddSingleton(Client)
                    .AddSingleton(Interactivity)
                    .AddSingleton<DownloadManager>()
                    .AddSingleton(this);
            if (Config.Voice.Enabled) services.AddSingleton(Voice);

            var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions()
            {
            });

            Commands = Client.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefixes = Config.CommandParser.Prefixes,
                CaseSensitive = Config.CommandParser.CaseSensitive,
                EnableDms = Config.CommandParser.UseDMs,
                EnableMentionPrefix = Config.CommandParser.UseMentionPrefix,
                IgnoreExtraArguments = Config.CommandParser.IgnoreExtraArgs,
                Services = serviceProvider
            });
            Commands.RegisterCommands<Commands>();
            // Don't use voice if not enabled
            if (Config.Voice.Enabled) Commands.RegisterCommands<VoiceCommands>();

            Client.ClientErrored += Client_ClientError;
            Client.Ready += Client_Ready;
        }

        private async Task Client_Ready(ReadyEventArgs e)
        {
            await Task.Yield();
        }

        public void Dispose()
        {
            Client.Dispose();
            Interactivity = null;
            Commands = null;
            Config = null;
        }

        private readonly string Author = "nike4613";
        private readonly string ProjectName = "DSharpBotCore";
        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
        public async Task RunAsync()
        {

            Client.DebugLogger.LogMessage(LogLevel.Info, Config.Name, $"Starting {Config.Name} ({Author}/{ProjectName} {Version})", DateTime.Now);

            await Client.ConnectAsync();

            // Stay alive
            while (!CTS.IsCancellationRequested)
                await Task.Delay(500);

            await Client.DisconnectAsync();

            Client.DebugLogger.LogMessage(LogLevel.Info, Config.Name, $"{Config.Name} ({Author}/{ProjectName} {Version}) stopped", DateTime.Now);
        }

        private Task Client_ClientError(ClientErrorEventArgs e)
        {
            // let's log the details of the error that just 
            // occured in our client
            e.Client.DebugLogger.LogMessage(LogLevel.Error, Config.Name, $"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}", DateTime.Now);

            // since this method is not async, let's return
            // a completed task, so that no additional work
            // is done
            return Task.CompletedTask;
        }

        private Configuration LoadConfiguration(string filename)
        {
            Configuration config;

            if (File.Exists(filename))
            {
                string configJson = File.ReadAllText(filename);
                config = JsonConvert.DeserializeObject<Configuration>(configJson);

                string reserialized = JsonConvert.SerializeObject(config, Formatting.Indented);
                if (reserialized != configJson) // ensure all properties exist in file
                    File.WriteAllText(filename, reserialized);
            }
            else
            {
                config = new Configuration
                {
                    Token = null
                };
                File.WriteAllText(filename, JsonConvert.SerializeObject(config, Formatting.Indented));
            }

            return config;
        }
    }
}
