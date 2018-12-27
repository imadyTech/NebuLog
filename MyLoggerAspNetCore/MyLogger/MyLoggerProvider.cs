using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;


namespace MyLogger
{

    public class MyLoggerProvider : ILoggerProvider
    {
        private readonly MyLoggerOption _option;

        public MyLoggerProvider(IOptions<MyLoggerOption> option)
        {
            _option = option.Value;
        }

        /// <summary>
        /// 创建ILogger实例
        /// </summary>
        /// <param name="categoryName">日志消息来源（例如xxController），其值为向依赖注入框架请求iLogger实例的对象名称。</param>
        /// <returns></returns>
        public ILogger CreateLogger(string categoryName)
        {
            return new MyLogger (_option, categoryName);
        }


        public void Dispose()
        {

        }
    }
}

