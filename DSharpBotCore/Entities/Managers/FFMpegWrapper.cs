using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DSharpBotCore.Entities.Managers
{
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

            var sanitisedNamePart = Regex.Replace(filename, invalidReStr, "_");
            foreach (var reservedWord in reservedWords)
            {
                var reservedWordPattern = string.Format("^{0}\\.", reservedWord);
                sanitisedNamePart = Regex.Replace(sanitisedNamePart, reservedWordPattern, "_reservedWord_.", RegexOptions.IgnoreCase);
            }

            return sanitisedNamePart;
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

        public LogLevel MessageLevel { get; set; }

        public interface IInput
        {
            bool IsPipe { get; }
            string InputString { get; }

            void StdInAvaliable(StreamWriter writer);
        }

        public class PipeInput : IInput
        {
            private BufferedPipe pipe;
            public PipeInput(BufferedPipe pipe)
            {
                this.pipe = pipe;
            }

            public bool IsPipe => true;

            public string InputString => "pipe:"; // pipe from stdin

            public void StdInAvaliable(StreamWriter writer)
            {
                pipe.Outputs += writer.BaseStream;
            }
        }

        public class FileInput : IInput
        {
            public FileInput(string filepath, string filename)
            {
                this.filepath = CoerceValidFilePath(filepath);
                this.filename = CoerceValidFileName(filename);
            }

            public bool IsPipe => false;

            private string filepath;
            private string filename;
            public string InputString => $@"""{Path.Combine(filepath, filename)}""";

            public void StdInAvaliable(StreamWriter writer)
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

            public void StdOutAvailiable(StreamReader reader)
            { // shouldn't be called since not flagged as a pipe
                throw new NotImplementedException();
            }
        }

        public IInput Input { get; set; }
        private EasyAddList<IOutput> outputs = new EasyAddList<IOutput>();
        public EasyAddList<IOutput> Outputs { get => outputs; set { } }

        private string ffmpegLocation;

        private Process ffmpegProcess;
        private Task ffmpegTask;

        public FFMpegWrapper(string ffmpegLocation = null)
        {
            this.ffmpegLocation = ffmpegLocation ?? "ffmpeg";
            MessageLevel = LogLevel.warning;
        }

        public Task AwaitProcessEnd => ffmpegTask;

        public void Start()
        {
            if ((ffmpegProcess != null && !ffmpegProcess.HasExited) || (ffmpegTask != null && !ffmpegTask.IsCompleted))
                throw new InvalidOperationException("Cannot have 2 processes running at a time");

            if (Input == null || Outputs.Count == 0)
                throw new InvalidOperationException("No inputs and/or outputs supplied");

            bool redirectStdIn = false;
            bool redirectStdOut = false;
            IOutput pipeOutput = null;
            string args = "";// $"-v {MessageLevel.ToString()}";

            var numOutPipes = Outputs.Count((IOutput op) => op.IsPipe);

            if (numOutPipes > 1)
                throw new InvalidOperationException("Only one output can be a pipe!");

            redirectStdIn = Input.IsPipe;
            redirectStdOut = numOutPipes != 0;

            pipeOutput = Outputs.FirstOrDefault((IOutput op) => op.IsPipe);

            args = $"-hide_banner -v {(redirectStdOut ? LogLevel.quiet : MessageLevel).ToString()} -i {Input.InputString} ";

            foreach (var op in outputs)
                args += $"{op.Options} {(op.Codec != null ? $"-c:a {op.Codec} " : "")}{(op.Format != null ? $"-f {op.Format} " : "")}{op.OutputString} ";

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
                Input.StdInAvaliable(ffmpegProcess.StandardInput);
            if (pipeOutput != null)
                pipeOutput.StdOutAvailiable(ffmpegProcess.StandardOutput);

            ffmpegTask = Task.Run(delegate
            {
                ffmpegProcess.WaitForExit();
            });
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
