using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using imady.NebuLog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Internal;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;//ASP.NET CORE 3.0
// Frank 2020.09.07 已过时：asp.net core 3.0
//using Microsoft.AspNetCore.Mvc;

namespace imady.NebuLogServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });
            services.AddCors();
            services
                .AddSignalR( )
                .AddJsonProtocol()
                .AddMessagePackProtocol();
                //.AddJsonProtocol();

            // Frank 2020.09.07 已过时：asp.net core 3.0
            //services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
            //services.AddMvc();
            //--- ASP.NET CORE 3.0
            services.AddControllersWithViews();

        }


        // Frank 2020.09.07 已过时：asp.net core 2.2
        //public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            //app.UseHttpsRedirection();
            app.UseStaticFiles();
            //app.UseCookiePolicy();

            //--- ASP.NET CORE 3.0
            app.UseRouting();
            app.UseCors();

            //app.UseAuthentication();
            //app.UseAuthorization();
            //Warning:
            //For most apps, calls to UseAuthentication, UseAuthorization, and UseCors 
            //must appear between the calls to UseRouting and UseEndpoints to be effective.
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<NebuLogHub>("/NebuLogHub");
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
            //--- ASP.NET CORE 3.0

            // Frank 2020.09.07 已过时：asp.net core 2.2
            /*
            app.UseSignalR(route =>
            {
                route.MapHub<NebuLogHub>("/NebuLogHub");
            });
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
            */
        }
    }
}
