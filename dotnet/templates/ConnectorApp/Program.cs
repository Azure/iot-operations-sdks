using Azure.Iot.Operations.Connector;
using ConnectorApp;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(DatasetSamplerFactory.DatasetSamplerFactoryProvider);
        services.AddHostedService<ConnectorWorker>();
        services.AddSingleton<string>("yourLeadershipPositionId");
    })
    .Build();

host.Run();
