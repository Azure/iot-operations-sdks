using Azure.Iot.Operations.Connector;
using Microsoft.Extensions.DependencyInjection;
using SqlQualityAnalyzerConnectorApp;

IHost host = Host.CreateDefaultBuilder(args)
    // .ConfigureLogging(logging =>
    // {
    //     logging.ClearProviders();
    //     logging.AddConsole(); // Outputs logs to console
    //     logging.AddDebug();   // Adds debug logging
    // })
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(SqlQualityAnalyzerDatasetSamplerFactory.DatasetSamplerFactoryProvider);
        services.AddSingleton(AssetMonitorFactoryProvider.AssetMonitorFactory);
        services.AddHostedService<TelemetryConnectorWorker>();
        //services.AddLogging();
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole(options => options.IncludeScopes = true);
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Trace); // Set to see all logs
        });
    })
    .Build();

host.Run();
