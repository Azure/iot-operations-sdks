using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.Assets;
using ConnectorApp;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(DatasetSamplerFactory.DatasetSamplerFactoryProvider);
        services.AddSingleton(AssetMonitorFactoryProvider.AssetMonitorFactory);
        services.AddHostedService<EventingTelemetryConnectorWorker>();
    })
    .Build();

EventingTelemetryConnectorWorker worker = host.Services.GetService<EventingTelemetryConnectorWorker>()!;

host.Run();
