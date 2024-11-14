using Azure.Iot.Operations.Connector;
using HttpServerConnectorApp;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(HttpDatasetSamplerFactory.HttpDatasetSourceFactoryProvider);
        services.AddHostedService<ConnectorAppWorker>();
        services.AddSingleton<string>("yourLeadershipPositionId");
    })
    .Build();

host.Run();
