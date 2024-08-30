using Azure.Iot.Operations.Services.SchemaRegistry.Host;

Console.WriteLine(License.MSLT);
Console.WriteLine($"{ThisAssembly.AssemblyName} {ThisAssembly.AssemblyInformationalVersion}\n\n");

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services
    .AddSingleton(MqttSessionClientFactoryProvider.MqttClientFactory)
    .AddTransient<SchemaValidator>()
    .AddTransient<SchemaRegistryService>()
    .AddHostedService<SchemaRegistryWorker>();

IHost host = builder.Build();
host.Run();

