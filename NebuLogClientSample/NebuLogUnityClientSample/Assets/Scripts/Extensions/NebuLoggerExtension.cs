using System;
using imady.NebuLog.Loggers;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class NebuLoggerExtension
{
    public static async void HandleUnityLogs(this INebuLogger logger, string message, string stackTrace, LogType type)
    {
        if (!logger.IsHubConnected)
            return;

        #region LogType/LogLevel comparison
        // --- enum UnityEngine.LogType | enum Microsoft.Extensions.Logging.LogLevel ------
        //                              |   Trace = 0,
        //  Log = 3,                    |   Debug = 1,
        //                              |   Information = 2,
        //  Warning = 2,                |   Warning = 3,
        //  Error = 0,                  |   Error = 4,
        //
        //  LogType used for Asserts. (These indicate an error inside Unity itself.)
        //  用于断言(Assert)的日志类型（这些表明Unity自身的一个错误）。
        //  Assert = 1,                 |   Critical = 5,
        //  Exception = 4               |   NebuLog.Exception (N/A in Microsoft)
        //                              |   none = 6
        //---------------------------------------------------------------------------------
        #endregion

        string nebumessage = StackTraceFormatter(message, stackTrace);
        var projectName = Application.productName;
        var categoryName = SceneManager.GetActiveScene().name;

        switch (type)
        {
            case LogType.Error:
                logger.LogInformation(nebumessage, "UnityError", projectName, categoryName);
                break;
            case LogType.Assert:
                logger.LogInformation(nebumessage, "UnityAssert", projectName, categoryName);
                break;
            case LogType.Warning:
                logger.LogInformation(nebumessage, LogLevel.Warning.ToString(), projectName, categoryName);
                break;
            case LogType.Log:
                logger.LogInformation(nebumessage, "UnityLog", projectName, categoryName);
                break;
            case LogType.Exception:
                //Unity的log回调不提供Exception类型参数。因此通过文本Exception类型接口调用。
                await logger.LogException(nebumessage, projectName, categoryName);
                break;
            default:
                logger.LogInformation(nebumessage, LogLevel.Debug.ToString(), projectName, categoryName);
                break;
        }
    }

    #region =====private methods 支撑方法=====
    //private string ExceptionFormatterResult;
    /// <summary>
    /// Exception的序列化
    /// </summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    public static string ExceptionFormatter(Exception arg)
    {
        var ExceptionFormatterResult = "";

        ExceptionFormatterResult += $"{arg.Message}</br>";
        ExceptionFormatterResult += $"Source: {arg.Source}</br>";
        ExceptionFormatterResult += $"StackTrace: {arg.StackTrace}</br> ";


        if (arg.InnerException != null)

        {
            var formatter = new Func<Exception, string>(ExceptionFormatter);
            ExceptionFormatterResult += formatter(arg.InnerException);
        }
        return ExceptionFormatterResult;
    }

    public static string StackTraceFormatter(string message, string stacktrace)
    {
        var StackTraceFormatterResult = string.Empty;
        //Nebulog客户端会将unity debug.log中的<方法名称>误认为xml,因此先替换掉
        stacktrace = stacktrace
            .Replace("<", "")
            .Replace(">", ".")
            //unity debug.log中包含换行字符,替换为br标记在HTML中显示比较友善.
            .Replace("\n", "<br>");

        StackTraceFormatterResult += $"{message}<br>";
        //StackTraceFormatterResult += $"Source: {Application.productName + "::" + SceneManager.GetActiveScene().name}<br>";
        StackTraceFormatterResult += stacktrace;

        return StackTraceFormatterResult;
    }
    #endregion

}
