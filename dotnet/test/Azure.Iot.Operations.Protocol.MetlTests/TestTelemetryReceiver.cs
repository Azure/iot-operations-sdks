// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Telemetry;

    public class TestTelemetryReceiver : TelemetryReceiver<string>
    {
        private readonly AsyncAtomicInt _telemetryCount;

        public async Task<int> GetTelemetryCountAsync()
        {
            return await _telemetryCount.ReadAsync().ConfigureAwait(false);
        }

        internal TestTelemetryReceiver(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, IPayloadSerializer payloadSerializer)
            : base(applicationContext, mqttClient, payloadSerializer)
        {
            _telemetryCount = new(0);
        }

        internal async Task TrackAsync()
        {
            await _telemetryCount.IncrementAsync().ConfigureAwait(false);
        }
    }
}
