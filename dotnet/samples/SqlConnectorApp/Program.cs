using Azure.Iot.Operations.Connector;
using Microsoft.Extensions.DependencyInjection;
using SqlQualityAnalyzerConnectorApp;

IHost host = Host.CreateDefaultBuilder(args)

    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(SqlQualityAnalyzerDatasetSamplerFactory.DatasetSamplerFactoryProvider);
        services.AddSingleton(AssetMonitorFactoryProvider.AssetMonitorFactory);
        services.AddHostedService<TelemetryConnectorWorker>();
    })
    .Build();

host.Run();
