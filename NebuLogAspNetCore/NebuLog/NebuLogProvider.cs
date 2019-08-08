using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;


namespace NebuLog
{

    public class NebuLogProvider : ILoggerProvider
    {
        private readonly NebuLogOption _option;

        public NebuLogProvider(IOptions<NebuLogOption> option)
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
            return new NebuLog (_option, categoryName);
        }


        public void Dispose()
        {

        }
    }
}

