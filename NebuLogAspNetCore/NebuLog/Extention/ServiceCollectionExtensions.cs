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
        /// <summary>
        /// 此方法适用于Asp.Net Core 
        /// （可能是因为CreateWebHostBuilder()或者CreateHostBuilder()在创建过程中配置了loggerFactory，而WPF没有此过程因此不会调用ILoggerProvider来产生实例，而是通过NebuLog默认的无参构造函数来生成实例。
        /// </summary>
        /// <param name="services"></param>
        public static void AddNebuLog(this IServiceCollection services)
        {
            //services.AddTransient<INebuLog, NebuLog>();
            services.AddScoped(typeof(INebuLogger<>), typeof(NebuLog<>));
            services.AddScoped<INebuLogger, NebuLogger>();
            /*
            services.AddTransient(typeof(INebuLog<>), (provider =>
            {
                var factory = provider.GetService<ILoggerFactory>();
                return factory.CreateLogger<NebuLog>();
            }));
            */
        }

        /// <summary>
        /// 此方法针对WPF等没有CreateWebHostBuilder()或者CreateHostBuilder()过程的框架。
        /// </summary>
        /// <param name="services"></param>
        /// <param name="provider"></param>
        public static void AddNebuLogFactory(this IServiceCollection services)
        {
            Func<IServiceProvider, NebuLogger> implementationFactory = (
                provider =>
                    provider.GetService<ILoggerFactory>()
                    .CreateLogger("WpfLogger") as NebuLogger
            );

            services.AddScoped<INebuLogger, NebuLogger>(implementationFactory);
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

}
