using Azure.Iot.Operations.Services.EdgeRegistry.Host;
using Azure.Iot.Operations.Protocol;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services
    .AddSingleton<ApplicationContext>()
    .AddSingleton(MqttSessionClientFactoryProvider.MqttClientFactory)
    .AddSingleton<EdgeRegistryService>()
    .AddHostedService<EdgeRegistryWorker>();

IHost host = builder.Build();
host.Run();
