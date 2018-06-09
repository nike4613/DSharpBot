using NYoutubeDL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DSharpBotCore.Entities
{
    public class DownloadManager
    {
        public class DownloadFile
        {
            protected DownloadManager parent;
            protected UInt64 id;
        }

        private YoutubeDL client;
        private Dictionary<UInt64, DownloadFile> files;

        public DownloadManager(Configuration config)
        {
            client = new YoutubeDL(config.Voice.Download.YoutubeDlLocation);

            client.Options.FilesystemOptions.Output = Path.Combine(config.Voice.Download.DownloadLocation, "%(id)s.%(extractor)s.%(ext)s");
            client.Options.PostProcessingOptions.ExtractAudio = true;
            client.Options.PostProcessingOptions.AudioQuality = "48k";
            client.Options.PostProcessingOptions.AudioFormat = config.Voice.Download.Format; // prefer WAV
            client.Options.PostProcessingOptions.FfmpegLocation = config.Voice.FFMpegLocation;
            client.Options.PostProcessingOptions.PreferFfmpeg = true;
        }


    }
}
