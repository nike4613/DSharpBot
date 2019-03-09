﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DSharpBotCore.Entities.Managers
{
    public class PlayQueue : BlockingCollection<PlayQueue.QueueEntry>
    {
        public sealed class QueueEntry
        {
            public string Format { get; set; } = "flac";

            private YoutubeDLWrapper.YTDLInfoStruct info;
            public YoutubeDLWrapper.YTDLInfoStruct Info
            {
                get => info;
                set
                {
                    info = value;
                    FileName = string.Format(YoutubeDLWrapper.YTDLInfoStruct.NameFormat, info.EntryID, info.ExtractorName) + "." + Format;
                    Title = info.Title;
                    Artist = info.Author;
                    Link = info.Url;
                    ArtistLink = info.AuthorUri;
                }
            }

            public string FileName { get; private set; }
            public string Title { get; private set; }
            public string Artist { get; private set; }

            public Uri Link { get; private set; }
            public Uri ArtistLink { get; private set; }

            public Action OnPlayStart;
            internal void InvokePlayStart() => OnPlayStart?.Invoke();
            public Action OnPlayEnd;
            internal void InvokePlayEnd() => OnPlayEnd?.Invoke();
            public Action<Exception> OnPlayError;
            internal void InvokePlayError(Exception e) => OnPlayError?.Invoke(e);

            public QueueEntry(YoutubeDLWrapper.YTDLInfoStruct s)
            {
                Info = s;
            }
        }

        private readonly Configuration.VoiceObject voiceConfig;

        public event Action<QueueEntry> PlayStart;
        public event Action<QueueEntry> PlayEnd;
        public event Action<QueueEntry, Exception> PlayError;

        public PlayQueue(Configuration config, YoutubeDLWrapper yt)
        {
            voiceConfig = config.Voice;
            ytdl = yt;
            Volume = voiceConfig.DefaultVolume;
        }

        public void Add(YoutubeDLWrapper.YTDLInfoStruct ytdlInfo, string format = "flac") =>
            Add(new QueueEntry(ytdlInfo) { Format = format });

        public void Add(IEnumerable<YoutubeDLWrapper.YTDLInfoStruct> infos, string format = "flac")
        {
            foreach (var item in infos)
                Add(item, format);
        }

        public void Clear()
        {
            PauseQueue();

            while (TryTake(out _, TimeSpan.Zero))
            {
            }

            ResumeQueue();
        }

        private FFMpegWrapper currentPlayer;
        private YoutubeDLWrapper ytdl;
        private BufferedPipe currentDiscordPipe;

        internal DiscordVoiceStream VoiceStream { get; private set; }

        private Thread playerThread;

        private double volume = 1;
        public double Volume
        {
            get => volume;
            set
            {
                volume = value;
                if (VoiceStream != null)
                    VoiceStream.Volume = value;
            }
        }

        internal void StartPlayer(DiscordVoiceStream stream)
        {
            if (playerThread == null)
            {
                VoiceStream = stream;
                stream.Volume = volume;

                stopToken = new CancellationTokenSource();
                nextToken = new CancellationTokenSource();

                playerThread = new Thread(PlayerThread);
                playerThread.Start(this);
            }
        }

        public void StopPlayer()
        {
            if (playerThread != null)
            {
                stopToken.Cancel();
                playerThread.Join();
                playerThread = null;

                VoiceStream.Close();
                VoiceStream = null;
            }
        }

        private CancellationTokenSource stopToken;
        private ManualResetEventSlim pauseEvent = new ManualResetEventSlim(true);

        public bool PauseQueue()
        {
            if (!pauseEvent.IsSet) return false;
            pauseEvent.Reset();
            return true;
        }

        public bool ResumeQueue()
        {
            if (pauseEvent.IsSet) return false;
            pauseEvent.Set();
            return true;
        }

        public bool PausePlaying()
        {
            if (currentDiscordPipe == null) return false;

            return currentDiscordPipe.Pause();
        }

        public bool ResumePlaying()
        {
            if (currentDiscordPipe == null) return false;

            return currentDiscordPipe.Resume();
        }

        private CancellationTokenSource nextToken;
        public void Next()
        {
            if (playerThread == null) throw new InvalidOperationException("Cannot move next if not in voice");

            nextToken.Cancel();
        }

        private static async void PlayerThread(object state)
        {
            var self = state as PlayQueue;
            Debug.Assert(self != null, nameof(self) + " != null");


            while (!self.stopToken.IsCancellationRequested)
            {
                var stopOrNext = CancellationTokenSource.CreateLinkedTokenSource(self.stopToken.Token, self.nextToken.Token);

                bool error = false;
                bool localFile = false;
                QueueEntry item = null;
                string filename = null;
                try
                {
                    if (!self.TryTake(out item, -1, stopOrNext.Token)) continue;

                    var ffmpeg = self.currentPlayer = new FFMpegWrapper(self.voiceConfig.FFMpegLocation);

                    localFile = File.Exists(filename = Path.Combine(self.voiceConfig.Download.DownloadLocation, item.FileName));

                    BufferedPipe ytdlPipe = null;
                    if (localFile)
                        ffmpeg.Input = new FFMpegWrapper.FileInput(self.voiceConfig.Download.DownloadLocation,
                                                                   item.FileName);
                    else
                    {
                        ytdlPipe = new BufferedPipe { BlockSize = 8192 }; // literally just piping from ytdl to ffmpeg
                        ffmpeg.Input = new FFMpegWrapper.PipeInput(ytdlPipe);
                        ffmpeg.Outputs.Add(new FFMpegWrapper.FileOutput(self.voiceConfig.Download.DownloadLocation,
                                                                       item.FileName, item.Format) { Options = "-ac 2 -ar 64k" });
                    }

                    await self.VoiceStream.VNext.SendSpeakingAsync();

                    var pipe = self.currentDiscordPipe = new BufferedPipe
                        {BlockSize = self.VoiceStream.BlockSize};
                    pipe.SetToken(stopOrNext.Token);
                    pipe.Outputs.Add(self.VoiceStream);
                    ffmpeg.Outputs.Add(new FFMpegWrapper.PipeOutput(pipe, "s16le") { Options = "-ac 2 -ar 48k", NormalizeVolume = !localFile });
                    ffmpeg.Start();

                    item.InvokePlayStart();
                    self.PlayStart?.Invoke(item);
                    
                    if (!localFile) await self.ytdl.StreamInItem(item.Info, ytdlPipe, stopOrNext.Token);
                    await pipe.AwaitEndOfStream;
                    self.currentDiscordPipe?.Close();
                    self.currentDiscordPipe = null;
                    await ffmpeg.AwaitProcessEnd;

                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception e)
                {
                    item?.InvokePlayError(e);
                    self.PlayError?.Invoke(item, e);
                    error = true;
                }
                finally
                {
                    if (!error)
                    {
                        item?.InvokePlayEnd();
                        if (item != null)
                            self.PlayEnd?.Invoke(item);
                    }

                    if (self.VoiceStream != null)
                        await self.VoiceStream.VNext.SendSpeakingAsync(false);

                    self.currentDiscordPipe?.Close();
                    self.currentDiscordPipe = null;
                    if (self.currentPlayer != null && self.currentPlayer.AwaitProcessEnd.IsCanceled)
                        await self.currentPlayer.Stop();
                    self.currentPlayer = null;

                    if (!localFile && filename != null && stopOrNext.IsCancellationRequested)
                        File.Delete(filename);
                }

                if (self.nextToken.IsCancellationRequested) // reset it
                    self.nextToken = new CancellationTokenSource();

                if (!self.pauseEvent.Wait(TimeSpan.FromMilliseconds(-1), self.stopToken.Token)) return;
            }
        }

    }
}