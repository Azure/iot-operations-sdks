using ConnectorApp;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(DatasetSourceFactory.DatasetSourceFactoryProvider);
        services.AddHostedService<ConnectorAppWorker>();
    })
    .Build();

host.Run();
