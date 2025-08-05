// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
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
            await using EchoStringStreamingCommandExecutor echoCommand = new(new ApplicationContext(), mock)
            {
                RequestTopicPattern = "mock/echo",
                OnStreamingCommandReceived = async (cancelableRequestStream, ct) =>
                {
                    CancellationTokenSource cts = new CancellationTokenSource();
                    CancellationToken ct = cts.Token;

                    List<string> requestStreamStrings = new();
                    await foreach (ExtendedRequest<string> requestStreamEntry in cancelableRequestStream.WithCancellation(ct))
                    {
                        requestStreamStrings.Add(requestStreamEntry.Request);
                    }

                    return StringStream(requestStreamStrings, ct);
                },
            };
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> StringStream(List<string> stringsToStream, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (string s in stringsToStream)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken); // Simulate asynchronous work
                yield return new StreamingExtendedResponse<string>()
                {
                    Response = s
                };
            }
        }
    }
}
