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
using Azure.Iot.Operations.Protocol.Streaming;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;

namespace Azure.Iot.Operations.Protocol.UnitTests
{
    public class StringStreamingCommandInvoker(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
        : StreamingCommandInvoker<string, string>(applicationContext, mqttClient, "someCommandName", new Utf8JsonSerializer())
    { }

    public class StreamingCommandInvokerUnitTests
    {
        [Fact]
        public async Task TestCancellingWhileStreamingRequests()
        {
            MockMqttPubSubClient mockClient = new("clientId", MqttProtocolVersion.V500);
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(1));
            CancellationToken ct = cts.Token;

            StringStreamingCommandInvoker testInvoker = new(new(), mockClient);

            ICancellableAsyncEnumerable<StreamingExtendedResponse<string>> responseStream = await testInvoker.InvokeStreamingCommandAsync(StringStream(), new StreamRequestMetadata(), new(), null, ct);

            responseStream.CancelAsync()

            // Cancellation token is still in effect during this loop due to how returned IAsyncEnumerable works
            await foreach (StreamingExtendedResponse<string> response in responseStream.AsyncEnumerable)
            {
                int index = response.StreamingResponseIndex;
                string payloadString = response.Response;
            }
        }

        [Fact]
        public async Task TestCancellingWhileStreamingResponses()
        {
            MockMqttPubSubClient mockClient = new("clientId", MqttProtocolVersion.V500);
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(1));
            CancellationToken ct = cts.Token;

            StringStreamingCommandInvoker testInvoker = new(new(), mockClient);

            ICancellableAsyncEnumerable<StreamingExtendedResponse<string>> responseStream = await testInvoker.InvokeStreamingCommandAsync(StringStream(), new StreamRequestMetadata(), new(), null, ct);

            // Cancellation token is still in effect during this loop due to how returned IAsyncEnumerable works
            await foreach (StreamingExtendedResponse<string> response in responseStream.AsyncEnumerable)
            {
                int index = response.StreamingResponseIndex;
                string payloadString = response.Response;
            }
        }


        private async IAsyncEnumerable<StreamingExtendedRequest<string>> StringStream()
        {
            for (int i = 0; i <= 10; i++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1)); // Simulate asynchronous work
                yield return new()
                {
                    Request = $"Message {i}",
                    StreamingRequestIndex = i,
                };
            }
        }
    }
}
