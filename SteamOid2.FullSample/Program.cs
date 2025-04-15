using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SteamOid2.API;
using System.Diagnostics;

namespace SteamOid2.FullSample;
internal class Program
{
    public static IHost Host { get; private set; } = null!;
    private static async Task Main(string[] args)
    {
        // this is how you set up a DI application from scratch. Host contains a service container which is just a list of service implementations
        HostBuilder builder = new HostBuilder();

        // set up logging
        builder.ConfigureLogging(l => l.AddSimpleConsole());

        if (Debugger.IsAttached)
        {
            // use the file in the directory instad of in the build folder
            Environment.CurrentDirectory = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, @"..\..\..\"));
        }

        // add appsettings.json as a source, and write defaults if it doesn't exist
        string dir = Path.Combine(Environment.CurrentDirectory, "Configuration");
        Directory.CreateDirectory(dir);
        if (!File.Exists(Path.Combine(dir, "appsettings.json")))
        {
            File.WriteAllText(Path.Combine(dir, "appsettings.json"), 
                """
                {
                  "OID2": {
                    "Realm": "http://localhost:8001/",
                    "CallbackUri": "http://localhost:8001/openid/login"
                  }
                }
                """);
        }

        builder.ConfigureHostConfiguration(configBuilder => configBuilder
            .SetBasePath(dir)
            .AddJsonFile("appsettings.json", false, true)
            .AddEnvironmentVariables());

        // run the ConfigureServices(...) method below, which adds which services to use.
        builder.ConfigureServices(ConfigureServices);

        // finalize the Host settings and start all the hosted services.
        Host = builder.Build();
        await Host.RunAsync();

        Host.Dispose();
    }
    private static void ConfigureServices(HostBuilderContext context, IServiceCollection serviceCollection)
    {
        // a hosted service has a Start and Stop method which is managed by the IHost.
        serviceCollection.AddHostedService<LoginHost>();

        // this says to create an ISteamOid2Client service using the implementation defined in SteamOid2Client
        serviceCollection.AddTransient<ISteamOid2Client, SteamOid2Client>();
    }
}
