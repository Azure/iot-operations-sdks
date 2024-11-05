using HttpThermostatConnectorAppProjectTemplate;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(ThermostatDatasetSamplerFactory.ThermostatDatasetSamplerFactoryProvider);
        services.AddHostedService<HttpServerConnectorAppWorker>();
    })
    .Build();

host.Run();
