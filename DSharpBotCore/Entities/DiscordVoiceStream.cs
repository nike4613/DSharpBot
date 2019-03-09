using DSharpPlus.VoiceNext;
using System;
using System.IO;

namespace DSharpBotCore.Entities
{
    class DiscordVoiceStream : Stream
    {
        private VoiceNextConnection vnext;

        public DiscordVoiceStream(VoiceNextConnection vnc)
        {
            vnext = vnc;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => 0;

        public override long Position { get => 0; set => throw new InvalidOperationException(); }

        public override void Flush()
        {
            //throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException();
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public VoiceNextConnection VNext => vnext; 

        private int blockSz = 3840;
        private int blockLen = 20;
        public int BlockSize { get => blockSz; set => blockSz = value; }
        public int BlockLength { get => blockLen; set => blockLen = value; }

        public bool UseEarRapeVolumeMode { get; set; }

        private double volume = 1;
        public double Volume { get => volume; set { volume = value; multCache = -1; } }

        private double multCache = -1;
        private double Multiplier
        {
            get
            {
                if (multCache <= 0)
                    multCache = (Math.Pow(10d, volume) - 1) / 9d;

                return multCache; // 卍
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            byte[] data = new byte[blockSz];

            var stream = vnext.GetTransmitStream(blockLen);

            for (int remain = count; remain > 0; remain -= data.Length)
            {
                int seglen = Math.Min(remain, data.Length);
                Buffer.BlockCopy(buffer, offset, data, 0, seglen);

                if (seglen < data.Length) // not a full sample, mute the rest
                    for (var i = seglen; i < data.Length; i++)
                        data[i] = 0;
                
                unsafe
                {
                    fixed (byte* dataPtr = data)
                    {
                        if (UseEarRapeVolumeMode)
                        {
                            for (int i = 0; i < data.Length; i++)
                                dataPtr[i] = (byte)(dataPtr[i] * Multiplier);
                        }
                        else
                        {
                            short* sharr = (short*)dataPtr;
                            for (int i = 0; i < data.Length / 2; i++)
                                sharr[i] = (short)(sharr[i] * Multiplier);
                        }
                    }
                }

                stream.Write(data, 0, blockSz);
                //vnext.SendAsync(data, blockLen).Wait();
            }
        }
    }
}
