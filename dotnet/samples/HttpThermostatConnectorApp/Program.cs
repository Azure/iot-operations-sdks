using HttpThermostatConnectorAppProjectTemplate;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(ThermostatDatasetSamplerFactory.DatasetSamplerFactoryProvider);
        services.AddHostedService<HttpThermostatConnectorAppWorker>();
    })
    .Build();

host.Run();
