using System;
using Newtonsoft.Json;

namespace DSharpBotCore.Entities
{
    public class Range
    {
        [JsonProperty("min")]
        public int? Min { get; set; } = null;
        [JsonProperty("max")]
        public int? Max { get; set; } = null;

        public bool InRange(int value)
            => (Min == null || value >= Min) && (Max == null || value <= Max);
        public int RandomInRange(Random rand)
            =>  Min != null ? 
                    (Max != null ? rand.Next(Min.Value, Max.Value)
                    : throw new InvalidOperationException("No upper bound to range"))
                : throw new InvalidOperationException("No lower bound to range");
    }
}
