using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DSharpBotCore.Entities.Managers
{
    class YoutubeDLWrapper
    {
        private string ytdlLoc;

        public YoutubeDLWrapper(string location)
        {
            ytdlLoc = location;
        }

        public struct YTDLInfoStruct
        {
            public const string NameFormat = "{0}.{1}";

            [JsonProperty("id", Required = Required.Always)]
            public string EntryID;
            [JsonProperty("extractor", Required = Required.Always)]
            public string ExtractorName;
            [JsonProperty("webpage_url_basename", Required = Required.Always)]
            public string UrlBasenme; // may be the same as entryID
            [JsonProperty("webpage_url", Required = Required.Always)]
            public string URL;

            public override string ToString()
            {
                return JsonConvert.SerializeObject(this);
            }
        }

        private Dictionary<string, YTDLInfoStruct> urlInfo = new Dictionary<string, YTDLInfoStruct>();
            
        public async Task<YTDLInfoStruct[]> GetUrlInfoStructs(string url)
        {
            if (urlInfo.ContainsKey(url))
            {
                return new YTDLInfoStruct[] { urlInfo[url] };
            }

            List<YTDLInfoStruct> infos = new List<YTDLInfoStruct>();

            await RunProcess($"-j {url}", reader => {
                string line;
                while ((line = reader.ReadLine()) != null) // line should be json
                {
                    Console.WriteLine(line);
                    infos.Add(JsonConvert.DeserializeObject<YTDLInfoStruct>(line));
                }
            });

            foreach (var info in infos)
                if (!urlInfo.ContainsKey(info.URL)) urlInfo.Add(info.URL, info);

            return infos.ToArray();
        }

        public async Task StreamInItem(YTDLInfoStruct item, BufferedPipe outputPipe)
        {
            await RunProcess($"-q -f bestaudio -o - {item.URL}", stream =>
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
                recieveOutput(proc.StandardOutput);
                proc.WaitForExit();
            });
        }
    }
}
