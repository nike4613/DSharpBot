using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DSharpBotCore.Modules
{
    // ReSharper disable once InconsistentNaming
    internal class FFController
    {

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        // ReSharper disable once InconsistentNaming
        public enum FFLogLevel
        {
            quiet,
            panic,
            fatal,
            error,
            warning,
            info,
            verbose,
            debug,
            trace,
        }

        private CancellationTokenSource cancel;
        private Process ffproc;

        private FFLogLevel logLevel;
        private string ffmpeg;

        public FFController(FFLogLevel level, string ffmpegLocation = null)
        {
            logLevel = level;
            ffmpeg = ffmpegLocation ?? Path.Combine(Environment.CurrentDirectory, "ffmpeg");
            IsPlaying = false;
        }

        private int channels = 2;    // for use with Discord 
        private int samples = 48000; // for use with Discord

        public bool IsPlaying { get; private set; }

        /// <summary>
        /// This should be unneccesary, but its here for safety.
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="sample"></param>
        public void SetOptions(int? channel = null, int? sample = null)
        {
            channels = channel ?? channels;
            samples = sample ?? samples;
        }

        public delegate Task PlayBuffer(byte[] data, int blockSize, int bitRate);

        private Task _playerTask;

        private async Task _PlayUsingAsync(string source, PlayBuffer player)
        {
            if (IsPlaying)
                throw new InvalidOperationException("Cannot play one thing while another is being played!");

            cancel = new CancellationTokenSource();

            var ffinfo = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = $@"-v {logLevel.ToString()} -i ""{source}"" -ac {channels} -f s16le -ar {samples} pipe:1",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            ffproc = Process.Start(ffinfo);
            if (ffproc != null)
            {
                var ffout = ffproc.StandardOutput.BaseStream;

                var buff = new byte[3840];
                int br;
                IsPlaying = true;
                while (!cancel.IsCancellationRequested && (br = ffout.Read(buff, 0, buff.Length)) > 0)
                {
                    if (br < buff.Length) // not a full sample, mute the rest
                        for (var i = br; i < buff.Length; i++)
                            buff[i] = 0;

                    await player(buff, 20, 16); // This is s16le PCM audio after all
                }
            }

            IsPlaying = false;
            if (cancel.IsCancellationRequested)
                ffproc?.Kill();
        }

        public async Task PlayUsingAsync(string source, PlayBuffer player) => await (_playerTask = _PlayUsingAsync(source, player));

        public Task PlayUsing(string source, PlayBuffer player) => PlayUsingAsync(source, player);

        public async Task Stop()
        {
            if (!IsPlaying)
                throw new InvalidOperationException("FFController is not currently playing anything!");
            cancel.Cancel();
            if (!_playerTask.Wait(TimeSpan.FromSeconds(1)))
                ffproc.Kill();
            await _playerTask;
        }
        public void StopAfter(TimeSpan delay)
        {
            if (!IsPlaying)
                throw new InvalidOperationException("FFController is not currently playing anything!");
            cancel.CancelAfter(delay);
        }
    }
}
