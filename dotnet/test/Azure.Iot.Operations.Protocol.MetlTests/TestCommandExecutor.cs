namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;
    using TestModel.dtmi_test_TestModel__1;

    public class TestCommandExecutor : CommandExecutor<Object_Test_Request, Object_Test_Response>
    {
        private AsyncAtomicInt executionCount;

        public async Task<int> GetExecutionCount()
        {
            return await executionCount.Read().ConfigureAwait(false);
        }

        internal TestCommandExecutor(IMqttPubSubClient mqttClient, string commandName)
            : base(mqttClient, commandName, new Utf8JsonSerializer())
        {
            executionCount = new(0);
        }

        public async Task Track()
        {
            await executionCount.Increment().ConfigureAwait(false);
        }
    }
}
