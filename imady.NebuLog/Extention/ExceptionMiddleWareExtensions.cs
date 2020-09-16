using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace imady.NebuLog
{

    public static class NebuLogExceptionMiddleWareExtensions
    {

        public static IApplicationBuilder UseNebuLogException(
            this IApplicationBuilder app, 
            NebuLogExceptionMiddleWareOption option,
            INebuLogger<NebuLogExceptionMiddleWare> logger)
        {
            return app.UseMiddleware<NebuLogExceptionMiddleWare>(option, logger);
        }
    }
}
