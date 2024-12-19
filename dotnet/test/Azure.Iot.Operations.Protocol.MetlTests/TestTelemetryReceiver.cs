// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Telemetry;
    using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;

    public class TestTelemetryReceiver : TelemetryReceiver<string>
    {
        private AsyncAtomicInt telemetryCount;

        public async Task<int> GetTelemetryCount()
        {
            return await telemetryCount.Read().ConfigureAwait(false);
        }

        internal TestTelemetryReceiver(IMqttPubSubClient mqttClient, string? telemetryName)
            : base(mqttClient, telemetryName, new Utf8JsonSerializer())
        {
            telemetryCount = new(0);
        }

        public async Task Track()
        {
            await telemetryCount.Increment().ConfigureAwait(false);
        }
    }
}
