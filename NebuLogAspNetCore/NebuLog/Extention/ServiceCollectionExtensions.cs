using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace NebuLog
{
    /// <summary>
    /// 
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        public static void AddNebuLog(this IServiceCollection services)
        {
            //services.AddTransient<INebuLog, NebuLog>();
            services.AddTransient(typeof(INebuLog<>), typeof( NebuLog<>));
            /*
            services.AddTransient(typeof(INebuLog<>), (provider =>
            {
                var factory = provider.GetService<ILoggerFactory>();
                return factory.CreateLogger<NebuLog>();
            }));
            */
        }
    }




    /// <summary>
    /// 这是原来通过扩展来给ILogger增加新方法的尝试。已过时。
    /// </summary>
    public static class NebuLogExtensionMethods
    {
        public static void LogCustom(this ILogger logger, string sender, string message)
        {
            logger.LogInformation(sender, message);
        }
    }

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
    }
}
