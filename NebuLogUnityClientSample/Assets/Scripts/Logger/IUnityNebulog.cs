using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityNebulog
{
    public interface IUnityNebulog
    {
        event EventHandler NebulogConnected;
        void HandleUnityLogs(string message, string stackTrace, LogType type);

        void LogMessage(string message, string loglevel, string projectName, string categoryName);
        
        void LogException(string exceptionMessage, string projectName, string categoryName);

        void LogException(Exception exception, string projectName, string categoryName);

        void LogException(Exception exception, string projectName, string categoryName, Func<Exception, string> formatter);

        void AddCustomStats(string statId, string statTitle, string color, string value);

        void LogCustomStats(string statId, string message);
    }
}
