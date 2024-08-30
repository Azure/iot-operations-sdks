using SampleCloudEvents;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services
    .AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory)
    .AddTransient(SchemaRegistryClientFactoryProvider.SchemaRegistryFactory)
    .AddTransient<OvenService>()
    .AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();
