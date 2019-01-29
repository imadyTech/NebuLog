using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.Text;

namespace MyLogger
{
    public static class MyLoggerExceptionMiddleWareExtensions
    {

        public static IApplicationBuilder UseMyLoggerException(
            this IApplicationBuilder app, 
            MyLoggerExceptionMiddleWareOption option,
            IMyLogger<MyLoggerExceptionMiddleWare> logger)
        {
            return app.UseMiddleware<MyLoggerExceptionMiddleWare>(option, logger);
        }
    }
}
