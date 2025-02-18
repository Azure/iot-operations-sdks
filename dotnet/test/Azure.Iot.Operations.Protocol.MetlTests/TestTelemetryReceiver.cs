// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Telemetry;

    public class TestTelemetryReceiver : TelemetryReceiver<string>
    {
        private AsyncAtomicInt _telemetryCount;

        public async Task<int> GetTelemetryCount()
        {
            return await _telemetryCount.Read().ConfigureAwait(false);
        }

        internal TestTelemetryReceiver(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, IPayloadSerializer payloadSerializer)
            : base(applicationContext, mqttClient, null, payloadSerializer)
        {
            _telemetryCount = new(0);
        }

        internal async Task Track()
        {
            await _telemetryCount.Increment().ConfigureAwait(false);
        }
    }
}
