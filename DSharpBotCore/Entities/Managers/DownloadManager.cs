using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DSharpBotCore.Entities.Managers
{
    public class DownloadManager
    {
        public class DownloadFile
        {
            protected internal DownloadManager Parent;

            protected internal ulong ID;

            protected internal string Filename;

            //protected internal DownloadInfo downloadInfo;
            protected internal Task DownloadTask;
            
        }

        private readonly Dictionary<ulong, DownloadFile> files = new Dictionary<ulong, DownloadFile>();

        private ulong currentId;

        public DownloadManager(Configuration config)
        {
            var downloadDir = config.Voice.Download.DownloadLocation;

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
                Parent = this,
                ID = currentId++,
            //    filename = @"pipe:" + poolEntry.PipeFilename,
            //    downloadInfo = dlInfo,
            //    downloadTask = dlTask
            };

            files.Add(file.ID, file);

            return file;
        }

    }
}
