using HttpServerConnectorApp;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(HttpDatasetSourceFactory.HttpDatasetSourceFactoryProvider);
        services.AddHostedService<HttpServerConnectorAppWorker>();
    })
    .Build();

host.Run();
