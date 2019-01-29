using Microsoft.Extensions.Logging;

namespace MyLogger
{
    public class MyLoggerOption
    {
        public string ProjectName { get; set; }

        public string MyLoggerHubUrl { get; set; }

        public LogLevel LogLevel { get; set; } = LogLevel.Trace;
    }
}