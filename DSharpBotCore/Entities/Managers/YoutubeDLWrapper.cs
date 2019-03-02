using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DSharpBotCore.Entities.Managers
{
    class YoutubeDLWrapper
    {
        private readonly string ytdlLoc;

        public YoutubeDLWrapper(string location)
        {
            ytdlLoc = location;
        }

        // ReSharper disable once InconsistentNaming
        public struct YTDLInfoStruct
        {
            public const string NameFormat = "{0}.{1}";

            [JsonProperty("id", Required = Required.Always)]
            public string EntryID;
            [JsonProperty("extractor", Required = Required.Always)]
            public string ExtractorName;
            [JsonProperty("webpage_url_basename", Required = Required.Always)]
            public string UrlBasename; // may be the same as entryID
            [JsonProperty("webpage_url", Required = Required.Always)]
            public Uri Url;
            [JsonProperty("thumbnail", Required = Required.DisallowNull)]
            public Uri Thumbnail;
            [JsonProperty("uploader", Required = Required.DisallowNull)]
            public string Author;
            [JsonProperty("uploader_url", Required = Required.DisallowNull)]
            public Uri AuthorUri;
            [JsonProperty("title", Required = Required.DisallowNull)]
            public string Title;

            public override string ToString()
            {
                return JsonConvert.SerializeObject(this);
            }
        }

        private Dictionary<Uri, YTDLInfoStruct> urlInfo = new Dictionary<Uri, YTDLInfoStruct>();
            
        public async Task<YTDLInfoStruct[]> GetUrlInfoStructs(Uri url)
        {
            if (urlInfo.ContainsKey(url))
            {
                return new[] { urlInfo[url] };
            }

            List<YTDLInfoStruct> infos = new List<YTDLInfoStruct>();

            await RunProcess($"-j {url}", reader => {
                string line;
                while ((line = reader.ReadLine()) != null) // line should be json
                {
                    Console.WriteLine("Got JSON Line");
                    infos.Add(JsonConvert.DeserializeObject<YTDLInfoStruct>(line));
                }
            });

            foreach (var info in infos)
                if (!urlInfo.ContainsKey(info.Url)) urlInfo.Add(info.Url, info);

            return infos.ToArray();
        }

        public async Task StreamInItem(YTDLInfoStruct item, BufferedPipe outputPipe)
        {
            await RunProcess($"-q -f bestaudio -o - {item.Url}", stream =>
            {
                outputPipe.Input = stream.BaseStream;
            });
        }

        private async Task RunProcess(string args, Action<StreamReader> recieveOutput)
        {
            var procInfo = new ProcessStartInfo
            {
                FileName = ytdlLoc,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };

            await Task.Run(delegate
            {
                var proc = Process.Start(procInfo);
                if (proc != null)
                {
                    recieveOutput(proc.StandardOutput);
                    proc.WaitForExit();
                }
            });
        }
    }
}
