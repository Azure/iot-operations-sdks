// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;

    public class TestCommandExecutor : CommandExecutor<string, string>
    {
        private AsyncAtomicInt executionCount;

        public async Task<int> GetExecutionCount()
        {
            return await executionCount.Read().ConfigureAwait(false);
        }

        internal TestCommandExecutor(IMqttPubSubClient mqttClient, string commandName, IPayloadSerializer payloadSerializer)
            : base(mqttClient, commandName, payloadSerializer)
        {
            executionCount = new(0);
        }

        internal async Task Track()
        {
            await executionCount.Increment().ConfigureAwait(false);
        }
    }
}
