using Azure.Iot.Operations.Services.StateStore;
using System.Reflection;
using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.DssCli;

public class Program
{
    public static void Main(string[] args)
    {
        string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Configuration
            .SetBasePath(assemblyFolder)
            .AddJsonFile("appsettings.json")
            .AddCommandLine(args)
            .AddEnvironmentVariables();
        builder.Services
            .AddSingleton<ApplicationContext>()
            .AddSingleton(MqttClientFactoryProvider.MqttClientFactory)
            .AddTransient(MqttClientFactoryProvider.StateStoreClientFactory)
            .AddHostedService<DssClientService>();

        IHost host = builder.Build();
        host.Run();
    }
}