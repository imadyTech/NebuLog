using Microsoft.Extensions.Logging;

namespace NebuLog
{
    public class NebuLogOption
    {
        public string ProjectName { get; set; }

        public string NebuLogHubUrl { get; set; }

        public LogLevel LogLevel { get; set; } = LogLevel.Trace;
    }
}