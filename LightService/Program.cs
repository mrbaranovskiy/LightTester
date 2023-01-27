using System.Runtime.InteropServices;
using LightService;

var builder = Host.CreateDefaultBuilder(args);

if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) builder = builder.UseSystemd();

IHost host = builder
    .ConfigureServices(services => { services.AddHostedService<Worker>(); })
    .Build();

await host.RunAsync();
