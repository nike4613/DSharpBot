using DSharpPlus.VoiceNext;
using System;
using System.Collections.Generic;
using System.Text;

namespace DSharpBotCore.Entities
{
    public class VolumeVoiceFilter : IVoiceFilter
    {
        private double volume = 1.0;
        public double Volume
        {
            get => volume;
            set
            {
                if (value > 4.0 || value < 0)
                    throw new ArgumentException("Value must be in the range [0, 4]", nameof(value));

                volume = value;
            }
        }

        public void Transform(Span<short> pcmData, AudioFormat pcmFormat, int duration)
        {
            foreach (ref short sample in pcmData)
                sample = (short)(sample * volume);
        }
    }
}
