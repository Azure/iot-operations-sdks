using HttpThermostatConnectorAppProjectTemplate;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(ThermostatDatasetSamplerFactory.ThermostatDatasetSamplerFactoryProvider);
        services.AddHostedService<HttpThermostatConnectorAppWorker>();
    })
    .Build();

host.Run();
