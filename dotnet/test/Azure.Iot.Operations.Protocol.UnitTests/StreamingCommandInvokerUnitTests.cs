// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            ICancelableAsyncEnumerable<StreamingExtendedResponse<string>> responseStream = testInvoker.InvokeStreamingCommandAsync(StringStream(), new(), new());

            await foreach (StreamingExtendedResponse<string> response in responseStream)
            {
                int index = response.StreamingResponseIndex;
                string payloadString = response.Response;
                await responseStream.CancelAsync(); // can cancel mid-stream
            }
        }

        private async IAsyncEnumerable<string> StringStream()
        {
            for (int i = 1; i <= 10; i++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1)); // Simulate asynchronous work
                yield return $"Message {i}";
            }
        }
    }
}
