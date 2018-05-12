using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DSharpBotCore.Modules
{
    class FFController
    {
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
        /// <param name="channels"></param>
        /// <param name="samples"></param>
        public void SetOptions(int? channels = null, int? samples = null)
        {
            this.channels = channels ?? this.channels;
            this.samples = samples ?? this.samples;
        }

        public delegate Task PlayBuffer(byte[] data, int blockSize, int bitRate);

        public async Task PlayUsingAsync(string source, PlayBuffer player)
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
            var ffproc = Process.Start(ffinfo);
            var ffout = ffproc.StandardOutput.BaseStream;

            var buff = new byte[3840];
            var br = 0;
            IsPlaying = true;
            while (!cancel.IsCancellationRequested && (br = ffout.Read(buff, 0, buff.Length)) > 0)
            {
                if (br < buff.Length) // not a full sample, mute the rest
                    for (var i = br; i < buff.Length; i++)
                        buff[i] = 0;

                await player(buff, 20, 16); // This is s16le PCM audio after all
            }
            IsPlaying = false;
            if (cancel.IsCancellationRequested)
                ffproc.Kill();
        }

        public Task PlayUsing(string source, PlayBuffer player) => PlayUsingAsync(source, player);

        public void Stop()
        {
            if (!IsPlaying)
                throw new InvalidOperationException("FFController is not currently playing anything!");
            cancel.Cancel();
        }
        public void StopAfter(TimeSpan delay)
        {
            if (!IsPlaying)
                throw new InvalidOperationException("FFController is not currently playing anything!");
            cancel.CancelAfter(delay);
        }

        public MultiPlayer GetMultiPlayer(PlayBuffer player)
        {
            var mp = new MultiPlayer(player, this);
            mp.Init();
            return mp;
        }

        public class MultiPlayer : IDisposable
        {
            PlayBuffer player;
            Process ffproc;
            FFController parent;
            Task playerTask;
            bool playing = false;

            protected internal MultiPlayer(PlayBuffer play, FFController parent)
            {
                this.parent = parent;
                player = play;
            }

            protected internal void Init() // create process
            {
                var ffinfo = new ProcessStartInfo
                {
                    FileName = parent.ffmpeg,
                    Arguments = $@"-v {parent.logLevel.ToString()} -f concat -i pipe: -ac {parent.channels} -f s16le -ar {parent.samples} pipe:1",
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false
                };
                ffproc = Process.Start(ffinfo);
            }

            public class PlayerEventArgs : EventArgs
            {

            }

            public event EventHandler<PlayerEventArgs> OnPlayStart;
            public event EventHandler<PlayerEventArgs> OnPlayStop;

            protected internal async Task PlayAudio()
            {
                var ffout = ffproc.StandardOutput.BaseStream;

                var buff = new byte[3840];
                var br = 0;
                playing = true;
                OnPlayStart(this, new PlayerEventArgs());
                while ((br = ffout.Read(buff, 0, buff.Length)) > 0)
                {
                    if (br < buff.Length) // not a full sample, mute the rest
                        for (var i = br; i < buff.Length; i++)
                            buff[i] = 0;

                    await player(buff, 20, 16); // This is s16le PCM audio after all
                }
                playing = false;
                OnPlayStop(this, new PlayerEventArgs());
            }

            public Task StartPlayer() => playerTask = PlayAudio();

            public void PlayFile(string filename)
            {
                var file = filename.Replace(@"\", @"\\").Replace(@"'", @"\'");

                ffproc.StandardInput.WriteLine($"file '{file}'");
            }

            public void Dispose()
            {
                if  (ffproc != null)
                {
                    ffproc.Kill();
                    ffproc.Dispose();
                }

                if (playerTask != null)
                {
                    playerTask.Wait(); // wait for player to finish up
                }
            }
        }
    }
}
