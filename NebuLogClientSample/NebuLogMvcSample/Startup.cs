using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using imady.NebuLog;

namespace NebuLogTestApp
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            Console.WriteLine("----------Startup------------");

        }

        public IConfiguration Configuration { get; }

        public IServiceCollection Services { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            Services = services;

            Services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });


            // Frank 2020.09.07 已过时：asp.net core 3.0
            //services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
            //services.AddMvc();
            //--- ASP.NET CORE 3.0
            services.AddControllersWithViews(options =>
                options.SuppressAsyncSuffixInActionNames = false);

            //===================================================================================
            Services.Configure<NebuLogOption>(Configuration.GetSection("NebuLogOption"));
            //如果想使用INebuLog的扩展则通过使用AddNebuLog
            Services.AddNebuLog();
            //设置系统日志输出的最小级别
            Services.AddLogging(builder =>
            {
                builder
                   //.AddConfiguration(Configuration.GetSection("NebuLogOption"))
                   // filter for all providers
                   //.AddFilter("System", LogLevel.Trace)
                   // Only for Debug logger, using the provider type or it's alias
                   //.AddFilter("Debug", LogLevel.Trace)
                   // Only for Console logger by provider type
                   .AddFilter<NebuLogProvider>("Microsoft", LogLevel.Trace)
                   .AddConsole()
                   .AddDebug();
            });
            //===================================================================================
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            //===================================================================================
            //如果想开启系统日志输出到INebuLog则使用以下代码
            var option = Services.BuildServiceProvider().GetService<IOptions<NebuLogOption>>();
            loggerFactory.UseNebuLog(Services);//extension方式添加NebuLog，两种写法效果等同
            //loggerFactory.AddProvider(new NebuLogProvider(option));
            //===================================================================================

            //--- ASP.NET CORE 3.0
            app.UseRouting();
            //app.UseCors();
            //app.UseAuthentication();
            //app.UseAuthorization();
            app.UseEndpoints(points =>
            {
                points.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });

            // Frank 2020.09.07 已过时：asp.net core 2.2
            /*
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
