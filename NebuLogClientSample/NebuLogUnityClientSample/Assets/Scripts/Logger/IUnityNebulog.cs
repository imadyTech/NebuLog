using System;
using UnityEngine;

[Obsolete ("2022-09-21 Please use imady.NebuLog.Loggers by methods in NebuLoggerExtension.cs")]
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
