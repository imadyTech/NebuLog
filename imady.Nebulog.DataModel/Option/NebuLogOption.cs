﻿using Microsoft.Extensions.Logging;

namespace imady.NebuLog.DataModel
{
    public class NebuLogOption
    {
        public string ProjectName { get; set; }

        public string NebuLogHubUrl { get; set; }

        public LogLevel LogLevel { get; set; } 
    }
}