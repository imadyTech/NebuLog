using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace imady.NebuLog.DataModel
{
    /// <summary>
    /// NebuLogExceptionMiddleWare中间件配置
    /// </summary>
    public class NebuLogExceptionMiddleWareOption
    {
        public NebuLogExceptionMiddleWareOption( string errorHandingPath = "")
        {
            ErrorHandingPath = errorHandingPath;
        }

        /// <summary>
        /// 错误跳转页面
        /// </summary>
        public PathString ErrorHandingPath { get; set; }
    }
}
