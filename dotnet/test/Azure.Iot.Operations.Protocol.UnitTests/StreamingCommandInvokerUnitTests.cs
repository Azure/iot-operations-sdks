// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            StringStreamingCommandInvoker testInvoker = new(new(), mockClient);

            IAsyncEnumerable<StreamingExtendedResponse<string>> responseStream = testInvoker.InvokeStreamingCommandAsync(StringStream(), new(), new(), null, ct);

            await foreach (StreamingExtendedResponse<string> response in responseStream.WithCancellation(ct))
            {
                int index = response.StreamingResponseIndex;
                string payloadString = response.Response;
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
