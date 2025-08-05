// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using System.Threading;
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
                OnStreamingCommandReceived = Handler
            };
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> Handler(IAsyncEnumerable<StreamingExtendedRequest<string>> requestStream, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // ct is the token that signals either that the command was cancelled by the invoker (or that
            // the command should no longer execute because it expired?)

            // These tokens are used to allow the executor side to cancel the stream at any time
            //
            // Throwing OperationCancelledException in this callback will tell the base class to
            // send the cancellation notification to the invoker side
            CancellationTokenSource executorAndInvokerSideCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationToken executorAndInvokerSideCancellationToken = executorAndInvokerSideCancellationTokenSource.Token;

            List<string> requestStreamStrings = new();
            await foreach (ExtendedRequest<string> requestStreamEntry in requestStream.WithCancellation(executorAndInvokerSideCancellationToken))
            {
                requestStreamStrings.Add(requestStreamEntry.Request);
            }

            foreach (string requestStreamString in requestStreamStrings)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1), executorAndInvokerSideCancellationToken); // Simulate asynchronous work
                yield return new StreamingExtendedResponse<string>()
                {
                    Response = requestStreamString
                };
            }
        }
    }
}
