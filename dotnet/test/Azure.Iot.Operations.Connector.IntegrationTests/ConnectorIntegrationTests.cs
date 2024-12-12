using Azure.Iot.Operations.Connector.UnitTests;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Services.Assets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

namespace Azure.Iot.Operations.Connector.IntegrationTests
{
    public sealed class ConnectorIntegrationTests
    {
        [Fact]
        public async Task ConnectorWorkerDoesLeaderElection()
        {
            Environment.SetEnvironmentVariable("AEP_CONFIGMAP_MOUNT_PATH", "./TestMountFiles");
            
            string cs = $"{Environment.GetEnvironmentVariable("MQTT_TEST_BROKER_CS")}";
            MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(cs);

            await File.WriteAllTextAsync("./TestMountFiles/aep_config/AEP_ADDITIONAL_CONFIGURATION", "{ \"leadershipPositionId\": \"" + Guid.NewGuid().ToString() + "\" }");
            await File.WriteAllTextAsync("./TestMountFiles/aep_config/AEP_TARGET_ADDRESS", mcs.HostName + ":" + mcs.TcpPort);

            await using MqttSessionClient sessionClient = new();
            TelemetryConnectorWorker worker = 
                new TelemetryConnectorWorker(
                    new Logger<TelemetryConnectorWorker>(new LoggerFactory()), 
                    sessionClient, 
                    new DatasetSamplerFactory(), 
                    new AssetMonitor());

            await worker.StartAsync(CancellationToken.None);
        }
    }
}
