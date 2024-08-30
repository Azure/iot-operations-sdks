using FaultableMqttBrokerWorkerService;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<FaultableMqttBrokerWorker>();
    })
    .Build();

host.Run();
