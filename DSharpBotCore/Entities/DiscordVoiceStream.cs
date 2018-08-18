using DSharpPlus.VoiceNext;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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

        private int blockSz = 3840;
        private int blockLen = 20;
        public int BlockSize { get => blockSz; set => blockSz = value; }
        public int BlockLength { get => blockLen; set => blockLen = value; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            byte[] data = new byte[blockSz];

            for (int remain = count; remain > 0; remain -= data.Length)
            {
                int seglen = Math.Min(remain, data.Length);
                Buffer.BlockCopy(buffer, offset, data, 0, seglen);

                if (seglen < data.Length) // not a full sample, mute the rest
                    for (var i = seglen; i < data.Length; i++)
                        data[i] = 0;

                vnext.SendAsync(data, blockLen, 16).Wait();
            }
        }
    }
}
