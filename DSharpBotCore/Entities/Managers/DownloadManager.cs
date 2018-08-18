using DSharpPlus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DSharpBotCore.Entities
{
    public class DownloadManager
    {
        public class DownloadFile
        {
            protected internal DownloadManager parent;

            protected internal UInt64 id;
            public ulong Id { get { return id; } }

            protected internal string filename;
            public string Filename { get { return filename; } }

            //protected internal DownloadInfo downloadInfo;
            protected internal Task downloadTask;
        }

        private Configuration config;
        private DiscordClient discordClient;
        //private YoutubeDLPool dlPool;
        private string downloadDir;
        private Dictionary<UInt64, DownloadFile> files = new Dictionary<ulong, DownloadFile>();

        private ulong currentId = 0;

        public DownloadManager(Configuration config, DiscordClient dclient)
        {
            discordClient = dclient;
            this.config = config;

            downloadDir = config.Voice.Download.DownloadLocation;

            //dlPool = new YoutubeDLPool(config, dclient, downloadDir);

            Directory.CreateDirectory(downloadDir);
        }

        /*private async Task<string> GetFilename(YoutubeDL client)
        {
            string name = "";

            EventHandler<string> stdoutHandle = delegate (object sender, string message)
            {
                name = message;
            };

            client.StandardOutputEvent += stdoutHandle;
            client.Options.VerbositySimulationOptions.GetFilename = true;
            await client.DownloadAsync();
            client.Options.VerbositySimulationOptions.GetFilename = false;
            client.StandardOutputEvent -= stdoutHandle;
            
            return name;
        } */

        public DownloadFile Download(string url)
        {
            //var poolEntry = dlPool.Get();
            /*var client = poolEntry.Client;
            client.VideoUrl = url;

            //var filename = await GetFilename(client);
            //client.Options.FilesystemOptions.Output = poolEntry.PipeName;
            discordClient.DebugLogger.LogMessage(LogLevel.Debug, "DownloadManager", client.PrepareDownload(), DateTime.Now);
            Task dlTask = client.DownloadAsync();
            DownloadInfo dlInfo = client.Info;

            // Mark my work as done (will never be reused until process dies)
            poolEntry.Release();
            */
            var file = new DownloadFile()
            {
                parent = this,
                id = currentId++,
            //    filename = @"pipe:" + poolEntry.PipeFilename,
            //    downloadInfo = dlInfo,
            //    downloadTask = dlTask
            };

            files.Add(file.id, file);

            return file;
        }

    }
}
