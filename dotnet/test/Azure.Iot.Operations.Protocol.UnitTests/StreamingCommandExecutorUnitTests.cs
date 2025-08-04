// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;

namespace Azure.Iot.Operations.Protocol.UnitTests
{
    public class EchoStringStreamingCommandExecutor : StreamingCommandExecutor<string, string>
    {
        public EchoStringStreamingCommandExecutor(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, string commandName = "echo")
            : base(applicationContext, mqttClient, commandName, new Utf8JsonSerializer())
        {

        }
    }

    public class StreamingCommandExecutorUnitTests
    {
        [Fact]
        public async Task MqttProtocolVersionUnknownThrowsException()
        {
            MockMqttPubSubClient mock = new(protocolVersion: MqttProtocolVersion.Unknown);
            TaskCompletionSource<Guid> executingCorrelationIdTcs = new();
            await using EchoStringStreamingCommandExecutor echoCommand = new(new ApplicationContext(), mock)
            {
                RequestTopicPattern = "mock/echo",
                OnStreamingCommandReceived = async (requestStream, correlationId, ct) =>
                {
                    executingCorrelationIdTcs.TrySetResult(correlationId);
                    List<string> requestStreamStrings = new();
                    CancellationTokenSource cts = new CancellationTokenSource();
                    await foreach (ExtendedRequest<string> requestStreamEntry in requestStream)
                    {
                        requestStreamStrings.Add(requestStreamEntry.Request);

                    }

                    return StringStream(requestStreamStrings);
                },
            };

            await echoCommand.CancelStreamingCommandAsync(await executingCorrelationIdTcs.Task);
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> StringStream(List<string> stringsToStream)
        {
            foreach (string s in stringsToStream)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1)); // Simulate asynchronous work
                yield return new StreamingExtendedResponse<string>()
                {
                    Response = s
                };
            }
        }
    }
}
