using imady.NebuLog.DataModel;
using imady.NebuLog.Loggers;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;


namespace imady.NebuLog.AspNetClient
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
            return new NebuLogger (_option, categoryName);
        }


        public void Dispose()
        {

        }
    }

    /// <summary>
    /// 这个是为了照顾WPF中国Hub Connection诡异的无法连接的问题 而另写的provider
    /// </summary>
    /// <param name="hubConnection"></param>
    public class NebuLogWpfProvider : ILoggerProvider
    {
        private readonly NebuLogOption _option;
        private readonly HubConnection _connection;
        public NebuLogWpfProvider(NebuLogOption nebulogOption, HubConnection hubConnection)
        {
            _option = nebulogOption;
            _connection = hubConnection;
        }
        /// <summary>
        /// 创建ILogger实例
        /// </summary>
        /// <param name="categoryName">日志消息来源（例如xxController），其值为向依赖注入框架请求iLogger实例的对象名称。</param>
        /// <returns></returns>
        public ILogger CreateLogger(string categoryName)
        {
            return new NebuLogger (_option, _connection, categoryName);
        }

        public void Dispose()
        {

        }
    }
}

