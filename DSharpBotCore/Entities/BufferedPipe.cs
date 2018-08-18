using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DSharpBotCore.Entities
{
    class BufferedPipe
    {
        private Stream input;
        private EasyAddList<Stream> outputs = new EasyAddList<Stream>();

        public EasyAddList<Stream> Outputs { get => outputs; set { } }

        private Task streamProcessorTask;

        public BufferedPipe()
        {

        }

        public Stream Input
        {
            set
            {
                input = value;
                if (streamProcessorTask == null)
                    streamProcessorTask = Task.Run((Action)_IOPortTask);
            }
        }

        public Task AwaitEndOfStream => streamProcessorTask;
        private int blockSize = 3840;
        public int BlockSize { get => blockSize; set => blockSize = value; }

        private void _IOPortTask()
        {
            byte[] buffer = new byte[blockSize];

            int amt = 0;

            try
            {
                while ((amt = input.Read(buffer, 0, buffer.Length)) > 0)
                    foreach (var output in outputs)
                        output.Write(buffer, 0, amt);
            }
            catch (ObjectDisposedException) { }
            finally
            {
                foreach (var output in outputs)
                    output.Close();
            }
        }

        public void Close()
        {
            if (input != null)
                input.Close();
        }

    }
}
