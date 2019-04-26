using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DSharpBotCore.Extensions;

namespace DSharpBotCore.Entities.Managers
{
    // ReSharper disable once InconsistentNaming
    class FFMpegWrapper
    {
        /// <summary>
        /// Strip illegal chars and reserved words from a candidate filename (should not include the directory path)
        /// </summary>
        /// <remarks>
        /// http://stackoverflow.com/questions/309485/c-sharp-sanitize-file-name
        /// </remarks>
        private static string CoerceValidFileName(string filename)
        {
            var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            var invalidReStr = string.Format(@"[{0}]+", invalidChars);

            var reservedWords = new[]
            {
                "CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4",
                "COM5", "COM6", "COM7", "COM8", "COM9", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4",
                "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            };

            var sanitizedNamePart = Regex.Replace(filename, invalidReStr, "_");
            foreach (var reservedWord in reservedWords)
            {
                var reservedWordPattern = string.Format("^{0}\\.", reservedWord);
                sanitizedNamePart = Regex.Replace(sanitizedNamePart, reservedWordPattern, "_reservedWord_.", RegexOptions.IgnoreCase);
            }

            return sanitizedNamePart;
        }
        private static string CoerceValidFilePath(string filename)
        {
            var invalidChars = Regex.Escape(new string(Path.GetInvalidPathChars()));
            var invalidReStr = string.Format(@"[{0}]+", invalidChars);

            var reservedWords = new[]
            {
                "CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4",
                "COM5", "COM6", "COM7", "COM8", "COM9", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4",
                "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            };

            var sanitisedNamePart = Regex.Replace(filename, invalidReStr, "_");
            foreach (var reservedWord in reservedWords)
            {
                var reservedWordPattern = string.Format("^{0}\\.", reservedWord);
                sanitisedNamePart = Regex.Replace(sanitisedNamePart, reservedWordPattern, "_reservedWord_.", RegexOptions.IgnoreCase);
            }

            return sanitisedNamePart;
        }

        public enum LogLevel
        {
            // ReSharper disable InconsistentNaming
            quiet,
            panic,
            fatal,
            error,
            warning,
            info,
            verbose,
            debug,
            trace,
            // ReSharper restore InconsistentNaming
        }

        public LogLevel MessageLevel { get; set; }

        public interface IInput
        {
            string Options { get; }

            bool IsPipe { get; }
            string InputString { get; }

            void StdInAvailable(StreamWriter writer);
        }

        public class StreamInput : Stream, IInput
        {
            public override void Flush()
            {
                stdinStream.Flush();
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                return stdinStream.BaseStream.FlushAsync(cancellationToken);
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

            public override void Write(byte[] buffer, int offset, int count)
            {
                stdinStream.BaseStream.Write(buffer, offset, count);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return stdinStream.BaseStream.WriteAsync(buffer, offset, count, cancellationToken);
            }

            public override void Close()
            {
                stdinStream.BaseStream.Close();
                base.Close();
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new InvalidOperationException();

            public override long Position
            {
                get => 0;
                set => throw new InvalidOperationException();
            }

            public string Options { get; set; }
            public bool IsPipe => true;
            public string InputString => "pipe:";

            private StreamWriter stdinStream;

            public void StdInAvailable(StreamWriter writer)
            {
                stdinStream = writer;
            }
        }

        public class PipeInput : IInput
        {
            public string Options => "";
            public bool IsPipe => true;

            public string InputString => "pipe:"; // pipe from stdin

            private BufferedPipe pipe;

            public PipeInput(BufferedPipe pipe)
            {
                this.pipe = pipe;
            }

            public void StdInAvailable(StreamWriter writer)
            {
                pipe.Outputs.Add(writer.BaseStream);
            }
        }

        public class FileInput : IInput
        {
            public string Options => "";

            public FileInput(string filepath, string filename)
            {
                this.filepath = CoerceValidFilePath(filepath);
                this.filename = CoerceValidFileName(filename);
            }

            public bool IsPipe => false;

            private string filepath;
            private string filename;
            public string InputString => $@"""{Path.Combine(filepath, filename)}""";

            public void StdInAvailable(StreamWriter writer)
            { // shouldn't be called since not flagged as a pipe
                throw new NotImplementedException();
            }
        }

        public interface IOutput
        {
            bool IsPipe { get; }
            string OutputString { get; }
            string Options { get; }
            string Format { get; }
            string Codec { get; }

            string Filters { get; }
            bool NormalizeVolume { get; }

            void StdOutAvailiable(StreamReader reader);
        }

        public class PipeOutput : IOutput
        {
            private BufferedPipe pipe;
            public PipeOutput(BufferedPipe pipe, string format, string codec = null)
            {
                this.pipe = pipe;
                Codec = codec;
                Format = format;
            }

            public bool IsPipe => true;

            public string OutputString => "pipe:"; // pipe to stdout

            public string Options { get; set; }

            public string Format { get; private set; }

            public string Codec { get; private set; }

            public string Filters { get; } = "";

            public bool NormalizeVolume { get; set; } = true;

            public void StdOutAvailiable(StreamReader reader)
            {
                pipe.Input = reader.BaseStream;
            }
        }

        public class FileOutput : IOutput
        {
            public FileOutput(string filepath, string filename, string format, string codec = null)
            {
                this.filepath = CoerceValidFilePath(filepath);
                this.filename = CoerceValidFileName(filename);
                Codec = codec;
                Format = format;
            }

            public bool IsPipe => false;

            private string filepath;
            private string filename;
            public string OutputString => $@"""{Path.Combine(filepath, filename)}""";

            public string Options { get; set; }

            public string Format { get; private set; }

            public string Codec { get; private set; }

            public string Filters { get; } = "";

            public bool NormalizeVolume { get; set; } = true;

            public void StdOutAvailiable(StreamReader reader)
            { // shouldn't be called since not flagged as a pipe
                throw new NotImplementedException();
            }
        }

        public IInput Input { get; set; }
        public List<IOutput> Outputs { get; } = new List<IOutput>();

        private readonly string ffmpegLocation;

        private Process ffmpegProcess;
        private Task ffmpegTask;

        public FFMpegWrapper(string ffmpegLocation = null)
        {
            this.ffmpegLocation = ffmpegLocation ?? "ffmpeg";
            MessageLevel = LogLevel.warning;
        }

        public Task AwaitProcessEnd => ffmpegTask;

        public void Start(CancellationToken token = default(CancellationToken))
        {
            if ((ffmpegProcess != null && !ffmpegProcess.HasExited) || (ffmpegTask != null && !ffmpegTask.IsCompleted))
                throw new InvalidOperationException("Cannot have 2 processes running at a time");

            if (Input == null || Outputs.Count == 0)
                throw new InvalidOperationException("No inputs and/or outputs supplied");

            var numOutPipes = Outputs.Count(op => op.IsPipe);

            if (numOutPipes > 1)
                throw new InvalidOperationException("Only one output can be a pipe!");

            var redirectStdIn = Input.IsPipe;
            var redirectStdOut = numOutPipes != 0;

            var pipeOutput = Outputs.FirstOrDefault(op => op.IsPipe);

            var args = $"-hide_banner -v {(redirectStdOut ? LogLevel.quiet : MessageLevel).ToString()} {Input.Options} -i {Input.InputString} ";

            foreach (var op in Outputs)
            {
                if (!string.IsNullOrWhiteSpace(op.Filters) || op.NormalizeVolume)
                    args +=
                        $"-filter_complex \"[0:a]{(op.NormalizeVolume ? "loudnorm" : "acopy")}[in];{(!string.IsNullOrWhiteSpace(op.Filters) ? op.Filters  : "[in]acopy[out]")}\" -map \"[out]\" ";
                args +=
                    $"{op.Options} {(op.Codec != null ? $"-c:a {op.Codec} " : "")}{(op.Format != null ? $"-f {op.Format} " : "")}{op.OutputString} ";
            }

            var ffinfo = new ProcessStartInfo
            {
                FileName = ffmpegLocation,
                Arguments = args,
                RedirectStandardOutput = redirectStdOut,
                RedirectStandardInput = redirectStdIn,
                UseShellExecute = false
            };

            ffmpegProcess = Process.Start(ffinfo);
            if (Input.IsPipe)
                Input.StdInAvailable(ffmpegProcess?.StandardInput);
            pipeOutput?.StdOutAvailiable(ffmpegProcess?.StandardOutput);

            ffmpegTask = ffmpegProcess.WaitForExitAsync(token);
        }

        public async Task Stop()
        {
            if (ffmpegProcess != null && !ffmpegProcess.HasExited)
            {
                ffmpegProcess.Kill();
                await ffmpegTask;
            }
            else
                throw new InvalidOperationException("FFMpeg Process not running");
        }

    }
}
