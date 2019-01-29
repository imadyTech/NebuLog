using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace MyLogger
{
    /// <summary>
    /// MyLoggerExceptionMiddleWare中间件配置
    /// </summary>
    public class MyLoggerExceptionMiddleWareOption
    {
        public MyLoggerExceptionMiddleWareOption( string errorHandingPath = "")
        {
            ErrorHandingPath = errorHandingPath;
        }

        /// <summary>
        /// 错误跳转页面
        /// </summary>
        public PathString ErrorHandingPath { get; set; }
    }
}
