using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

[Obsolete("Try to switch to NetStandard 2.1 but failed.")]
internal class StartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        /* configure any services you need here */
    }

    public void Configure(IApplicationBuilder app)
    {
        // Output a "hello world" to the user who accesses the server
        app.Use(async (context, next) =>
        {
            await context.Response.WriteAsync("Hello, world!");
        });
    }
}
