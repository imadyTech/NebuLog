using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.Text;

namespace NebuLog
{
    public static class NebuLogExceptionMiddleWareExtensions
    {

        public static IApplicationBuilder UseNebuLogException(
            this IApplicationBuilder app, 
            NebuLogExceptionMiddleWareOption option,
            INebuLog<NebuLogExceptionMiddleWare> logger)
        {
            return app.UseMiddleware<NebuLogExceptionMiddleWare>(option, logger);
        }
    }
}
