// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;

    public class TestCommandExecutor : CommandExecutor<string, string>
    {
        private readonly AsyncAtomicInt _executionCount;

        public async Task<int> GetExecutionCountAsync()
        {
            return await _executionCount.ReadAsync().ConfigureAwait(false);
        }

        internal TestCommandExecutor(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, string commandName, IPayloadSerializer payloadSerializer)
            : base(applicationContext, mqttClient, commandName, payloadSerializer)
        {
            _executionCount = new(0);
        }

        internal async Task TrackAsync()
        {
            await _executionCount.IncrementAsync().ConfigureAwait(false);
        }
    }
}
