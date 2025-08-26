// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Streaming;
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
        public async Task Test()
        {
            MockMqttPubSubClient mock = new(protocolVersion: MqttProtocolVersion.Unknown);
            await using EchoStringStreamingCommandExecutor echoCommand = new(new ApplicationContext(), mock)
            {
                RequestTopicPattern = "mock/echo",
                OnStreamingCommandReceived = Handler
            };

            await echoCommand.StartAsync();

        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> Handler(IAsyncEnumerable<StreamingExtendedRequest<string>> requestStream, ICancelableStreamContext streamContext, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // ct is the token that signals either that the command was cancelled by the invoker (or that
            // the command should no longer execute because it expired?)

            List<string> requestStreamStrings = new();
            await foreach (StreamingExtendedRequest<string> requestStreamEntry in requestStream.WithCancellation(cancellationToken))
            {
                // can cancel while streaming requests
                await streamContext.CancelAsync(default);

                requestStreamStrings.Add(requestStreamEntry.Request);
            }

            foreach (string requestStreamString in requestStreamStrings)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1)); // Simulate asynchronous work

                // can cancel while streaming responses
                await streamContext.CancelAsync(default);

                yield return new StreamingExtendedResponse<string>()
                {
                    Response = requestStreamString
                };
            }
        }
    }
}
