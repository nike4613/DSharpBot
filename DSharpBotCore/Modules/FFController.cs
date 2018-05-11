using System;
using System.Collections.Generic;
using System.Text;

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

        public FFController(FFLogLevel level, string ffmpegLocation = "ffmepg")
        {

        }
    }
}
