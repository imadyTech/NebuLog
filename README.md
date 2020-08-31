# NebuLog - Nebula Logger</br>
A distributed logging tool based on Microsoft SignalR, to help developers debugging their projects.</br>


Update README.md
2020-08-31 Update</br>
There was a new feature added for INebuLog interface: Custom Status monitor.

        /// <summary>
        /// Adding a dynamic status monitor at the right side Custom Stats panel.
        /// </summary>
        /// <param name="statId">The Id of the custom stats（must be an unique value otherwise it will be conflicted afterward.）</param>
        /// <param name="statTitle">The title of the custom stat.</param>
        /// <param name="color">The color you want the stat to display in the panel.</param>
        void AddCustomStats(string statId, string statTitle, string color);

        /// <summary>
        /// Update the stat.
        /// </summary>
        /// <param name="statId">The Id of the custom stat. </param>
        /// <param name="message">The value to display. </param>
        void LogCustomStats(string statId, string message);
Keep in mind these methods only supported in INebuLog so you might need cast the ILogger to INebuLog, for example:
~~~
        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
            (_logger as INebuLog).AddCustomStats("customStat", "customStat", "green");
        }
~~~


2019-08-08 Update</br>
MyLogger has been renamed to NebuLog.
Now you can use Nuget to install the NebuLog instead of refer the source code:
~~~
PM> Install-Package imady.NebuLog -Version 0.1.0
~~~

2019-01-28 Update</br>
Now the ILogger is extended to IMyLogger if you want to extend some new features more than Microsoft's standard Logxxx().
This is the updated code in Startup.cs:
~~~
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            ...
            Services.Configure<MyLoggerOption>(Configuration.GetSection("MyLoggerOption"));
            services.AddMyLogger();
            Services.AddLogging( builder =>
            {
                builder
                    .AddFilter<MyLoggerProvider>("Microsoft", LogLevel.Trace)
                    .AddConsole()
                    .AddDebug();
            });
            ...
        }
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            ...
if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                //app.UseBrowserLink();
                app.UseDatabaseErrorPage();
            }
            else
            {
                //app.UseExceptionHandler("/Home/Error");
                //this will add the MyLoggerExceptionMiddleWare to intercept any application level exception and logging to MyLogger:
                var myLogger = Services.BuildServiceProvider().GetService<IMyLogger<MyLoggerExceptionMiddleWare>>();
                app.UseMyLoggerException(
                    new MyLoggerExceptionMiddleWareOption(errorHandingPath: "/home/error"),
                    myLogger);
            }

            var option = Services.BuildServiceProvider().GetService< IOptions<MyLoggerOption>>();
            loggerFactory.AddProvider(new MyLoggerProvider(option));
            ...
        }
~~~


How to use</br>
-----------------
1. Have the MyLoggerServer project deployed to cloud or running on local;
2. Add MyLogger folder (a VS project) to your solution folder (not yet support Nuget installation);
3. Refer MyLogger project to the applicaiton that to realtime log;
4. Add the targeted Url to appsettings.json:
~~~ Json
  "MyLoggerOption": {
    "MyLoggerHubUrl": "http://localhost:5000/MyLoggerHub",
    "LogLevel": "Trace"
  }
~~~
5. In Startup.cs, add the MyLogger service to the DI framework:
~~~
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            ...
            Services.Configure<MyLoggerOption>(Configuration.GetSection("MyLoggerOption"));
            Services.AddLogging( builder =>
            {
                builder
                    .AddFilter<MyLoggerProvider>("Microsoft", LogLevel.Trace)
                    .AddConsole()
                    .AddDebug();
            });
            ...
        }
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            ...
            var option = Services.BuildServiceProvider().GetService< IOptions<MyLoggerOption>>();
            loggerFactory.AddProvider(new MyLoggerProvider(option));
            ...
        }
~~~
6. Now you can use ILogger<> as usual in your application, e.g. the Controller:
~~~
        public IActionResult Index()
        {
            _logger.LogTrace("LogTrace.");
            _logger.LogDebug("LogDebug.");
            _logger.LogInformation("LogInformation.");
            _logger.LogWarning("LogWarning.");
            _logger.LogError("LogError.");
            _logger.LogCritical("LogCritical.");
            return View();
        }
~~~
Then the loggins will be streamed to the MyLoggerServer webpage and you may easily debug your applications now.
</br>
Alternatively you may use our cloud deployed MyLogger service Free of charge (currently in developing) just simply refer the MyLogger and config our MyLoggerHub url in appsettings.json.
</br>

![MyLoggerScreenShot](https://github.com/silentrock/MyLogger/blob/master/MyLoggerScreenShot.png)
</br></br>

Issues:
-------
1. The remote side logging was designed for development time debugging purpose instead of production logging, e.g. operation data logging with persistence needs. However we may have the MyLogger upgraded in the future to support more features.
2. Currently the monitoring web page was realized base on Bootstrap-Datatable, which could be slowed down when bulk amount of logs come to the server side (we suggest click Refresh button above the datatable to clear the data when the web page frame rate decrease to observable level). We will optimize the code in the future to improve the user experience.
3. Currently only Asp.net core is supported. As Microsoft has released SignalR Client for JavaScript and Java as well, we will add support to these languages in the future. Other languages and platforms are subject to the progress of Microsoft SignalR.
</br>
You are welcomed to feedback for comments and advices or fork the MyLogger project.
</br>
</br>
</br>




Credit:</br>
Admin template powered by [Charisma](http://usman.it/themes/charisma)</br>
Datatable powered by [Bootstrap Table](https://github.com/wenzhixin/bootstrap-table/)</br>
