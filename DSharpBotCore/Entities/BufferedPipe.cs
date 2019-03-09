using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DSharpBotCore.Entities
{
    public class BufferedPipe
    {
        private Stream input;
        private List<Stream> outputs = new List<Stream>();
        private CancellationTokenSource stopToken = new CancellationTokenSource();
        private CancellationTokenSource tokenSource;

        public BufferedPipe()
        {
            tokenSource = stopToken;
        }

        public List<Stream> Outputs
        {
            get => outputs;
            set => outputs = value;
        }

        private Task streamProcessorTask;

        public Stream Input
        {
            set
            {
                input = value;
                if (streamProcessorTask == null)
                    streamProcessorTask = Task.Run((Action)_IOPortTask, tokenSource.Token);
            }
        }

        public Task AwaitEndOfStream => streamProcessorTask;
        private int blockSize = 3840;
        public int BlockSize { get => blockSize; set => blockSize = value; }

        private readonly ManualResetEventSlim pauseEvent = new ManualResetEventSlim(true);

        public void SetToken(CancellationToken token)
        {
            tokenSource = CancellationTokenSource.CreateLinkedTokenSource(stopToken.Token, token);
        }

        public bool Pause()
        {
            if (!pauseEvent.IsSet) return false;
            pauseEvent.Reset();
            return true;
        }

        public bool Resume()
        {
            if (pauseEvent.IsSet) return false;
            pauseEvent.Set();
            return true;
        }

        private void _IOPortTask()
        {
            byte[] buffer = new byte[blockSize];

            try
            {
                int amt;
                while ((amt = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (tokenSource.IsCancellationRequested) break;
                    foreach (var output in outputs)
                        output.Write(buffer, 0, amt);
                    pauseEvent.Wait(tokenSource.Token);
                }
            }
            catch (EndOfStreamException)
            {
            }
            catch (IOException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                foreach (var output in outputs)
                    output.Close();
            }
        }

        public void Close()
        {
            if (input != null)
            {
                input.Close();
                tokenSource.Cancel();
            }
        }

    }
}
