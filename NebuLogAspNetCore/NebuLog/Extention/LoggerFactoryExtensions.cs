using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace NebuLog
{
    /// <summary>
    /// ......
    /// </summary>
    public static class LoggerFactoryExtensions
    {
        public static void UseNebuLog(this ILoggerFactory factory, IServiceCollection services)
        {
            factory.AddProvider(new NebuLogProvider(services.BuildServiceProvider().GetService<IOptions<NebuLogOption>>()));
        }

        //通过ILoggerFactory工厂模式也可以创建iLogger实例，
        //参见 https://www.cnblogs.com/artech/p/inside-net-core-logging-2.html
        ///两个方法创建的Logger在日志记录行为上是等效的
        /*
        public static INebuLog<T> CreateLogger<T>(this ILoggerFactory factory)
        {
            return factory.CreateLogger<T>();
        }*/
        //public static ILogger CreateLogger(this ILoggerFactory factory, Type type)；

        public static void UseNebuLog<T>(this ILoggerFactory factory, IServiceCollection services)
        {
            factory.AddProvider(new NebuLogProvider(services.BuildServiceProvider().GetService<IOptions<NebuLogOption>>()));

        }


        public static void UseNebuLogWpf(this ILoggerFactory factory, NebuLogOption nebulogOption, HubConnection hubConnection)
        {

            factory.AddProvider(new NebuLogWpfProvider(nebulogOption, hubConnection));
        }
        public static void UseNebuLogWpf<T>(this ILoggerFactory factory, NebuLogOption nebulogOption, HubConnection hubConnection)
        {

            factory.AddProvider(new NebuLogWpfProvider(nebulogOption, hubConnection));
        }
    }
}
