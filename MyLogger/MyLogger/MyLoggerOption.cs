using Microsoft.Extensions.Logging;

namespace MyLogger
{
    public class MyLoggerOption
    {
        public string MyLoggerHubUrl { get; set; }

        public LogLevel LogLevel { get; set; } = LogLevel.Trace;
    }
}