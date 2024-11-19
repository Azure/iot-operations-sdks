using Azure.Iot.Operations.Connector;
using RestThermostatConnector;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(RestThermostatDatasetSamplerFactory.RestDatasetSourceFactoryProvider);
        services.AddHostedService<ConnectorWorker>();
        services.AddSingleton<string>("yourLeadershipPositionId");
    })
    .Build();

host.Run();
