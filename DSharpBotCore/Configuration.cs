using System;
using System.Collections.Generic;
using System.Text;
using DSharpPlus;
using DSharpPlus.Interactivity;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace DSharpBotCore
{
    public class Configuration
    {
        [JsonProperty("token", Required = Required.AllowNull)]
        public string Token;

        [JsonProperty("prefix", Required = Required.DisallowNull)]
        public string Prefix = "!";

        [JsonProperty("log", Required = Required.DisallowNull)]
        public bool Log = true;

        [JsonProperty("logLevel", Required = Required.DisallowNull), JsonConverter(typeof(StringEnumConverter), false)]
        public LogLevel LogLevel = LogLevel.Warning;
        
        public class InteractivityObject
        {
            [JsonProperty("timeout", Required = Required.DisallowNull), JsonConverter(typeof(TimeSpanConverter))]
            public TimeSpan Timeout = TimeSpan.FromMinutes(5);
            
            public class PaginationObject
            {
                [JsonProperty("timeout", Required = Required.DisallowNull), JsonConverter(typeof(TimeSpanConverter))]
                public TimeSpan Timeout = TimeSpan.FromMinutes(5);

                [JsonProperty("behaviour", Required = Required.DisallowNull), JsonConverter(typeof(StringEnumConverter))]
                public TimeoutBehaviour Behaviour = TimeoutBehaviour.Default;
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
                [JsonProperty("errorPersist", Required = Required.DisallowNull), JsonConverter(typeof(TimeSpanConverter))]
                public TimeSpan ErrorPersistTime = TimeSpan.FromSeconds(5);

                [JsonProperty("deleteResponses", Required = Required.DisallowNull)]
                public bool DeleteResponses = true;
            }

            [JsonProperty("poll", Required = Required.DisallowNull)]
            public PollObject Poll = new PollObject();

            public class RollObject
            {
                public class D6Object
                {
                    [JsonProperty("useSpecial", Required = Required.DisallowNull)]
                    public bool UseSpecial = false;

                    [JsonProperty("rolls", Required = Required.DisallowNull)]
                    public int Rolls = 25; // 2.5s

                    [JsonProperty("rollInterval", Required = Required.DisallowNull), JsonConverter(typeof(TimeSpanConverter))]
                    public TimeSpan RollInterval = TimeSpan.FromMilliseconds(100); // .1s

                    [JsonProperty("faces", Required = Required.DisallowNull)]
                    public List<string> FaceEmoji;
                }

                [JsonProperty("d6", Required = Required.DisallowNull)]
                public D6Object D6 = new D6Object();

                [JsonProperty("deleteTrigger", Required = Required.DisallowNull)]
                public bool DeleteTrigger = false;
            }

            [JsonProperty("roll", Required = Required.DisallowNull)]
            public RollObject Roll = new RollObject();
        }

        [JsonProperty("commands", Required = Required.DisallowNull)]
        public CommandsObject Commands = new CommandsObject();
    }

    class TimeSpanConverter : JsonConverter<TimeSpan>
    {
        public override bool CanRead => true;

        public override bool CanWrite => true;

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

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

        public override string ToString()
        {
            return base.ToString();
        }

        public override void WriteJson(JsonWriter writer, TimeSpan value, JsonSerializer serializer)
        {
            JToken.FromObject(value.ToString(timeFormat)).WriteTo(writer);
        }
    }
}
