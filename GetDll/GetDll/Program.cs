


using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;



[Obsolete ("Try to switch to NetStandard 2.1 but failed.")]
public class Program
{
    static IWebHost _host;
    public static void Main()
    {
        _host= WebHost
            .CreateDefaultBuilder()
            .UseStartup<StartUp>()
            .UseUrls("http://*:5999")
            .Build();

        _host.Run();
    }
}