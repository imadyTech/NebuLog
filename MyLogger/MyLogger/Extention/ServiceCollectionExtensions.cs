using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace MyLogger
{
    /// <summary>
    /// 
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        public static void AddMyLogger(this IServiceCollection services)
        {
            services.AddTransient<IMyLogger, MyLogger>();
        }


    }

    /// <summary>
    /// ......
    /// </summary>
    public static class LoggerFactoryExtensions
    {
        public static void UseMyLogger(this ILoggerFactory factory, IServiceCollection services)
        {
            factory.AddProvider(new MyLoggerProvider(services.BuildServiceProvider().GetService<IOptions<MyLoggerOption>>()));
        }

        //通过ILoggerFactory工厂模式也可以创建iLogger实例，
        //参见 https://www.cnblogs.com/artech/p/inside-net-core-logging-2.html
        ///两个方法创建的Logger在日志记录行为上是等效的
        //public static ILogger<T> CreateLogger<T>(this ILoggerFactory factory)
        //public static ILogger CreateLogger(this ILoggerFactory factory, Type type)；
    }
}
