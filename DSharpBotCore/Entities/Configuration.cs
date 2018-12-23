using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DSharpPlus;
using DSharpPlus.Interactivity;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace DSharpBotCore.Entities
{
    public class Configuration
    {

        [JsonProperty("name", Required = Required.Always)]
        public string Name = "DSharpPlus Bot";

        [JsonProperty("token", Required = Required.AllowNull)]
        public string Token;

        [JsonProperty("log", Required = Required.DisallowNull)]
        public bool Log = true;

        [JsonProperty("logLevel", Required = Required.DisallowNull), JsonConverter(typeof(StringEnumConverter), false)]
        public LogLevel LogLevel = LogLevel.Warning;

        [JsonProperty("libraryPath", Required = Required.DisallowNull)]
        public string LibraryPath = "libs";

        [JsonProperty("botMode", Required = Required.DisallowNull), JsonConverter(typeof(StringEnumConverter))]
        public BotMode BotMode = BotMode.Icons__DND;

        public class ErrorsObject
        {
            [JsonProperty("persist", Required = Required.DisallowNull), JsonConverter(typeof(TimeSpanConverter))]
            public TimeSpan PersistTime = TimeSpan.FromSeconds(5);

            [JsonProperty("inlineInfo", Required = Required.DisallowNull)]
            public bool InlineInfo = true;
        }

        [JsonProperty("errors", Required = Required.DisallowNull)]
        public ErrorsObject Errors = new ErrorsObject();

        public class ConnectionOptions
        {
            [JsonProperty("autoReconnect", Required = Required.DisallowNull)]
            public bool AutoReconnect = true;
        }

        [JsonProperty("connection", Required = Required.DisallowNull)]
        public ConnectionOptions Connection = new ConnectionOptions();

        public class CommandParserObject
        {
            [JsonProperty("prefixes", Required = Required.DisallowNull)]
            public string[] Prefixes = { "!" };

            [JsonProperty("caseSensitive", Required = Required.DisallowNull)]
            public bool CaseSensitive;

            [JsonProperty("useDms", Required = Required.DisallowNull)]
            public bool UseDMs;

            // Doesn't seem to work
            [JsonProperty("useMentionPrefix", Required = Required.DisallowNull)]
            public bool UseMentionPrefix = true;
            
            [JsonProperty("ignoreExtraArgs", Required = Required.DisallowNull)]
            public bool IgnoreExtraArgs = true;
        }

        [JsonProperty("commandParser", Required = Required.DisallowNull)]
        public CommandParserObject CommandParser = new CommandParserObject();

        public class VoiceObject
        {
            [JsonProperty("enabled", Required = Required.DisallowNull)]
            public bool Enabled;

            [JsonProperty("ffmpegLocation", Required = Required.DisallowNull)]
            // ReSharper disable once InconsistentNaming
            public string FFMpegLocation = "libs/ffmpeg";

            public class DownloadObject
            {
                /*[JsonProperty("format", Required = Required.DisallowNull), JsonConverter(typeof(StringEnumConverter), false)]
                public NYoutubeDL.Helpers.Enums.AudioFormat Format = NYoutubeDL.Helpers.Enums.AudioFormat.wav;*/

                [JsonProperty("ydlLocation", Required = Required.DisallowNull)]
                public string YoutubeDlLocation = "libs/youtube-dl";

                /*[JsonProperty("ffprobeLocation", Required = Required.DisallowNull)]
                public string FFProbeLocation = "libs/ffprobe";*/

                [JsonProperty("downloadLocation", Required = Required.DisallowNull)]
                public string DownloadLocation = "audio";

                [JsonProperty("logLevel", Required = Required.DisallowNull), JsonConverter(typeof(StringEnumConverter), false)]
                public LogLevel LogLevel = LogLevel.Info;
            }

            [JsonProperty("download", Required = Required.DisallowNull)]
            public DownloadObject Download = new DownloadObject();

            [JsonProperty("defaultVolume", Required = Required.DisallowNull)]
            public double DefaultVolume = 1;
        }

        [JsonProperty("voice", Required = Required.DisallowNull)]
        public VoiceObject Voice = new VoiceObject();

        public class InteractivityObject
        {
            [JsonProperty("timeout", Required = Required.DisallowNull), JsonConverter(typeof(TimeSpanConverter))]
            public TimeSpan Timeout = TimeSpan.FromMinutes(5);
            
            public class PaginationObject
            {
                [JsonProperty("timeout", Required = Required.DisallowNull), JsonConverter(typeof(TimeSpanConverter))]
                public TimeSpan Timeout = TimeSpan.FromMinutes(5);

                [JsonProperty("behaviour", Required = Required.DisallowNull), JsonConverter(typeof(StringEnumConverter))]
                public TimeoutBehaviour Behaviour = TimeoutBehaviour.DeleteMessage;
            }

            [JsonProperty("pagination", Required = Required.DisallowNull)]
            public PaginationObject Pagination = new PaginationObject();
        }

        [JsonProperty("interactivity", Required = Required.DisallowNull)]
        public InteractivityObject Interactivity = new InteractivityObject();
        
        public class CommandsObject
        {
            public class PollObject
            {
                [JsonProperty("deleteTrigger", Required = Required.DisallowNull)]
                public bool DeleteTrigger = true;

                [JsonProperty("deleteResponses", Required = Required.DisallowNull)]
                public bool DeleteResponses = true;

                [JsonProperty("updateTime", Required = Required.DisallowNull)]
                public bool UpdateTime = true;

                // unused for a number of reasons
                [JsonProperty("textSeperator", Required = Required.DisallowNull)]
                public string TextSeperator = ";;";

                // unused for a number of reasons
                [JsonProperty("optionSeperator", Required = Required.DisallowNull)]
                public string OptionSeperator = ";";
            }

            [JsonProperty("poll", Required = Required.DisallowNull)]
            public PollObject Poll = new PollObject();

            public class RollObject
            {
                public class D6Object
                {
                    [JsonProperty("useSpecial", Required = Required.DisallowNull)]
                    public bool UseSpecial;

                    [JsonProperty("rolls", Required = Required.DisallowNull)]
                    public int Rolls = 25; // 2.5s

                    [JsonProperty("rollInterval", Required = Required.DisallowNull), JsonConverter(typeof(TimeSpanConverter))]
                    public TimeSpan RollInterval = TimeSpan.FromMilliseconds(100); // .1s

                    [JsonProperty("faces", Required = Required.Default)]
                    public List<string> FaceEmoji = new List<string>();
                }

                [JsonProperty("d6", Required = Required.DisallowNull)]
                public D6Object D6 = new D6Object();

                public class GenesysSymbolsObject
                {
                    [JsonProperty("none", Required = Required.DisallowNull)]
                    public ulong None;
                    [JsonProperty("success", Required = Required.DisallowNull)]
                    public ulong Success;
                    [JsonProperty("advantage", Required = Required.DisallowNull)]
                    public ulong Advantage;
                    [JsonProperty("triumph", Required = Required.DisallowNull)]
                    public ulong Triumph;
                    [JsonProperty("failure", Required = Required.DisallowNull)]
                    public ulong Failure;
                    [JsonProperty("threat", Required = Required.DisallowNull)]
                    public ulong Threat;
                    [JsonProperty("despair", Required = Required.DisallowNull)]
                    public ulong Despair;
                }

                [JsonProperty("genesysSymbols")]
                public GenesysSymbolsObject GenesysSymbols;

                public class GenesysDiceObject
                {
                    [JsonProperty("boost", Required = Required.DisallowNull)]
                    public ulong Boost;
                    [JsonProperty("setback", Required = Required.DisallowNull)]
                    public ulong Setback;
                    [JsonProperty("ability", Required = Required.DisallowNull)]
                    public ulong Ability;
                    [JsonProperty("difficulty", Required = Required.DisallowNull)]
                    public ulong Difficulty;
                    [JsonProperty("proficiency", Required = Required.DisallowNull)]
                    public ulong Proficiency;
                    [JsonProperty("challenge", Required = Required.DisallowNull)]
                    public ulong Challenge;
                }

                [JsonProperty("genesysDice")]
                public GenesysDiceObject GenesysDice;

                public class ReactionObject
                {
                    [JsonProperty("confirm", Required = Required.DisallowNull)]
                    public string Confirm;
                    [JsonProperty("cancel", Required = Required.DisallowNull)]
                    public string Cancel;
                }

                [JsonProperty("reactions")]
                public ReactionObject Reactions;

                [JsonProperty("deleteTrigger", Required = Required.DisallowNull)]
                public bool DeleteTrigger;
            }

            [JsonProperty("roll", Required = Required.DisallowNull)]
            public RollObject Roll = new RollObject();

            public class CalcObject
            {
                [JsonProperty("deleteTrigger", Required = Required.DisallowNull)]
                public bool DeleteTrigger = true;
            }

            [JsonProperty("calc", Required = Required.DisallowNull)]
            public CalcObject Calc = new CalcObject();
        }

        [JsonProperty("commands", Required = Required.DisallowNull)]
        public CommandsObject Commands = new CommandsObject();
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum BotMode
    {
        Icons__DND,
        None,
        Genesys
    }

    class TimeSpanConverter : JsonConverter<TimeSpan>
    {
        public override bool CanRead => true;

        public override bool CanWrite => true;

        private readonly string timeFormat = @"h\hm\mss\.FFF\s";
        public override TimeSpan ReadJson(JsonReader reader, Type objectType, TimeSpan existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            bool success = TimeSpan.TryParseExact(reader.Value.ToString(), timeFormat, null, out TimeSpan ts);
            if (!success)
            {
                Console.Error.WriteLine($"Error parsing TimeSpan from configuration; falling back to {existingValue.ToString(timeFormat)}");
                ts = existingValue;
            }
            return ts;
        }

        public override void WriteJson(JsonWriter writer, TimeSpan value, JsonSerializer serializer)
        {
            JToken.FromObject(value.ToString(timeFormat)).WriteTo(writer);
        }
    }
}
