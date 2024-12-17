using ConnectionManagementSample;
using SampleClient;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddHostedService<LibraryManagedConnectionWorker>();
    })
    .Build();

host.Run();
