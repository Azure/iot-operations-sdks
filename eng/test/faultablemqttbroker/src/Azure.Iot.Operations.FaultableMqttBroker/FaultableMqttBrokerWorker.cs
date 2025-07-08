using Azure.Iot.Operations.Protocol.Tools;

namespace FaultableMqttBrokerWorkerService
{
    public class FaultableMqttBrokerWorker : BackgroundService
    {
        private readonly ILogger<FaultableMqttBrokerWorker> _logger;

        public FaultableMqttBrokerWorker(ILogger<FaultableMqttBrokerWorker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            FaultableMqttBroker broker = new FaultableMqttBroker(1884);
            await broker.StartAsync();
        }
    }
}
