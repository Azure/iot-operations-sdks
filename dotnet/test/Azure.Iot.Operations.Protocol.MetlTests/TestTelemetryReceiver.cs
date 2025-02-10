// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Telemetry;

    public class TestTelemetryReceiver : TelemetryReceiver<string>
    {
        private AsyncAtomicInt telemetryCount;

        public async Task<int> GetTelemetryCount()
        {
            return await telemetryCount.Read().ConfigureAwait(false);
        }

        internal TestTelemetryReceiver(IMqttPubSubClient mqttClient, IPayloadSerializer payloadSerializer)
            : base(mqttClient, null, payloadSerializer)
        {
            telemetryCount = new(0);
        }

        internal async Task Track()
        {
            await telemetryCount.Increment().ConfigureAwait(false);
        }
    }
}
