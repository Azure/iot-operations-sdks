using Azure.Iot.Operations.ConnectorSample;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using Azure.Iot.Operations.Services.IntegrationTest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Iot.Operations.Services.IntegrationTests
{
    public class ConnectorAppIntegrationTests
    {
        // This test simply asserts that the connector app defined in the HttpThermostatConnectorApp project is running as expected. It does this by checking to
        // see that telemetry is being pushed by the connector app. If any part of the connector deployment fails, or if the connector cannot reach the asset,
        // then it won't send any telemetry.
        [Fact]
        public async Task CheckIfHttpThermostatConnectorSampleIsRunning()
        { 
            MqttSessionClient sessionClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync(Guid.NewGuid().ToString());

            MqttClientSubscribeOptions options = new MqttClientSubscribeOptions("/mqtt/machine/status");

            TaskCompletionSource<MqttApplicationMessageReceivedEventArgs> receivedMessageTcs = new();
            sessionClient.ApplicationMessageReceivedAsync += (args) =>
            {
                receivedMessageTcs.TrySetResult(args);
                args.AutoAcknowledge = true;
                return Task.CompletedTask;
            };

            await sessionClient.SubscribeAsync(options);

            try
            {
                var receivedMessage = await receivedMessageTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
                ThermostatStatus? status = JsonSerializer.Deserialize<ThermostatStatus>(receivedMessage.ApplicationMessage.PayloadSegment);
                Assert.NotNull(status);
                Assert.Equal("91", status.DesiredTemparature);
                Assert.Equal("85", status.ActualTemperature);
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for a thermostat status message to be received. The sample connector app isn't working as expected.");
            }

            Assert.Fail("Hello");
        }
    }
}
