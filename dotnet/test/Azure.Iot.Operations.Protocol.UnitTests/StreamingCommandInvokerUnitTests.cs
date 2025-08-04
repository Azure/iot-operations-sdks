// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;

namespace Azure.Iot.Operations.Protocol.UnitTests
{
    public class StringStreamingCommandInvoker(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
        : StreamingCommandInvoker<string, string>(applicationContext, mqttClient, "someCommandName", new Utf8JsonSerializer())
    { }

    public class StreamingCommandInvokerUnitTests
    {
        [Fact]
        public async Task Test()
        {
            MockMqttPubSubClient mockClient = new("clientId", MqttProtocolVersion.V500);
            StringStreamingCommandInvoker testInvoker = new(new(), mockClient);

            CommandRequestMetadata requestMetadata = new CommandRequestMetadata();
            IAsyncEnumerable<StreamingExtendedResponse<string>> responseStream = testInvoker.InvokeStreamingCommandAsync(StringStream(testInvoker, requestMetadata.CorrelationId), requestMetadata, new(), null, default);

            await foreach (StreamingExtendedResponse<string> response in responseStream)
            {
                int index = response.StreamingResponseIndex;
                string payloadString = response.Response;

                // Can cancel while streaming response
                await testInvoker.CancelStreamingCommandAsync(requestMetadata.CorrelationId);
            }
        }

        private async IAsyncEnumerable<string> StringStream(StreamingCommandInvoker<string, string> invoker, Guid correlationId)
        {
            for (int i = 1; i <= 10; i++)
            {
                if (i == 5)
                {
                    // Can cancel while streaming response
                    await invoker.CancelStreamingCommandAsync(correlationId);

                    //Allow users to cancel their own stream this way?
                    throw new OperationCanceledException();
                }

                await Task.Delay(TimeSpan.FromMilliseconds(1)); // Simulate asynchronous work
                yield return $"Message {i}";
            }
        }
    }
}
