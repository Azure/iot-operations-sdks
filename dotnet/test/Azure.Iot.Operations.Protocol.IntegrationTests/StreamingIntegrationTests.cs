// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Streaming;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;

namespace Azure.Iot.Operations.Protocol.IntegrationTests
{
    public class StreamingIntegrationTests
    {
        // shared across all tests in this file, but each test should use a unique GUID as the correlationId of the stream
        private readonly ConcurrentDictionary<Guid, List<StreamingExtendedRequest<string>>> _receivedRequests = new();
        private readonly ConcurrentDictionary<Guid, List<StreamingExtendedResponse<string>>> _sentResponses = new();

        public class StringStreamingCommandInvoker(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            : StreamingCommandInvoker<string, string>(applicationContext, mqttClient, "someCommandName", new Utf8JsonSerializer())
        { }

        public class EchoStringStreamingCommandExecutor : StreamingCommandExecutor<string, string>
        {
            public EchoStringStreamingCommandExecutor(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, string commandName = "echo")
                : base(applicationContext, mqttClient, commandName, new Utf8JsonSerializer())
            {

            }
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public async Task StreamRequestsAndResponsesInSerial(bool multipleRequests, bool multipleResponses)
        {
            await using MqttSessionClient invokerMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient executorMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();

            int requestCount = multipleRequests ? 3 : 1;
            int responseCount = multipleResponses ? 3 : 1;

            EchoStringStreamingCommandExecutor executor = multipleResponses
                ? new(new(), executorMqttClient)
                 {
                     OnStreamingCommandReceived = SerialHandlerMultipleResponses
                 }
                : new(new(), executorMqttClient)
                {
                    OnStreamingCommandReceived = SerialHandlerSingleResponse
                };

            await executor.StartAsync();

            StringStreamingCommandInvoker invoker = new(new(), invokerMqttClient);

            StreamRequestMetadata requestMetadata = new();
            var responseStream = await invoker.InvokeStreamingCommandAsync(GetStringRequestStream(requestCount), requestMetadata);

            List<StreamingExtendedResponse<string>> receivedResponses = new();
            await foreach (StreamingExtendedResponse<string> response in responseStream.AsyncEnumerable)
            {
                receivedResponses.Add(response);
            }

            List<StreamingExtendedRequest<string>> expectedRequests = new();
            await foreach (var request in GetStringRequestStream(requestCount))
            {
                expectedRequests.Add(request);
            }

            if (!_receivedRequests.TryGetValue(requestMetadata.CorrelationId, out var receivedRequests))
            {
                Assert.Fail("Executor did not receive any requests");
            }

            Assert.Equal(expectedRequests.Count, receivedRequests.Count);
            for (int i = 0; i < expectedRequests.Count; i++)
            {
                Assert.Equal(expectedRequests[i].Request, receivedRequests[i].Request);
            }

            if (!_sentResponses.TryGetValue(requestMetadata.CorrelationId, out var sentResponses))
            {
                Assert.Fail("Executor did not send any responses");
            }

            Assert.Equal(receivedResponses.Count, sentResponses.Count);
            for (int i = 0; i < expectedRequests.Count; i++)
            {
                Assert.Equal(sentResponses[i].Response, receivedResponses[i].Response);
            }
        }

        [Fact]
        public async Task InvokerCanCancelWhileStreamingRequests()
        {
            await using MqttSessionClient invokerMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient executorMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();

            EchoStringStreamingCommandExecutor executor =
                new(new(), executorMqttClient)
                {
                    OnStreamingCommandReceived = SerialHandlerSingleResponse
                };

            await executor.StartAsync();

            StringStreamingCommandInvoker invoker = new(new(), invokerMqttClient);

            StreamRequestMetadata requestMetadata = new();
            var responseStream = await invoker.InvokeStreamingCommandAsync(GetStringRequestStreamWithDelay(), requestMetadata);

            await responseStream.CancelAsync();
        }

        [Fact]
        public async Task InvokerCanCancelWhileStreamingResponses()
        {
            await using MqttSessionClient invokerMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient executorMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();

            EchoStringStreamingCommandExecutor executor =
                new(new(), executorMqttClient)
                {
                    OnStreamingCommandReceived = SerialHandlerMultipleResponsesWithDelay
                };

            await executor.StartAsync();

            StringStreamingCommandInvoker invoker = new(new(), invokerMqttClient);

            StreamRequestMetadata requestMetadata = new();
            var responseStream = await invoker.InvokeStreamingCommandAsync(GetStringRequestStream(1), requestMetadata);

            await foreach (var response in responseStream.AsyncEnumerable)
            {
                //TODO check first response

                await responseStream.CancelAsync();
            }
        }

        [Fact]
        public async Task ExecutorCanCancelWhileStreamingRequests()
        {
            await using MqttSessionClient invokerMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient executorMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();

            EchoStringStreamingCommandExecutor executor =
                new(new(), executorMqttClient)
                {
                    OnStreamingCommandReceived = SerialHandlerWithCancellation
                };

            await executor.StartAsync();

            StringStreamingCommandInvoker invoker = new(new(), invokerMqttClient);

            StreamRequestMetadata requestMetadata = new();
            var responseStream = await invoker.InvokeStreamingCommandAsync(GetStringRequestStream(1), requestMetadata);

            bool receivedCancellation = false;
            try
            {
                await foreach (var response in responseStream.AsyncEnumerable)
                {
                    //TODO care about the one expected response?
                }
            }
            catch (AkriMqttException ame) when (ame.Kind is AkriMqttErrorKind.Cancellation)
            {
                receivedCancellation = true;
            }

            Assert.True(receivedCancellation);
        }

        [Fact]
        public Task ExecutorCanCancelWhileStreamingResponses()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public Task CanStreamRequestsAndResponsesSimultaneously()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public Task CanAddUserPropertiesToSpecificToMessagesInRequestAndResponseStreams()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public Task CanCancelFromInvokerSideWithCancellationToken()
        {
            throw new NotImplementedException();
        }

        private async IAsyncEnumerable<StreamingExtendedRequest<string>> GetStringRequestStream(int requestCount)
        {
            for (int i = 0; i < requestCount; i++)
            {
                await Task.Delay(TimeSpan.FromMicroseconds(1)); // Simulate asynchronous work
                yield return new()
                {
                    Request = $"Message {i}",
                    StreamingRequestIndex = i,
                };
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> GetStringResponseStream(int responseCount)
        {
            for (int i = 0; i < responseCount; i++)
            {
                await Task.Delay(TimeSpan.FromMicroseconds(1)); // Simulate asynchronous work
                yield return new()
                {
                    Response = $"Message {i}",
                    StreamingResponseIndex = i,
                };
            }
        }

        private async IAsyncEnumerable<StreamingExtendedRequest<string>> GetStringRequestStreamWithDelay()
        {
            for (int i = 0; i <= 10; i++)
            {
                yield return new()
                {
                    Request = $"Message {i}",
                    StreamingRequestIndex = i,
                };

                await Task.Delay(TimeSpan.FromHours(1)); // Simulate asynchronous work that is stuck after the first request is sent
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> GetStringResponseStreamWithDelay()
        {
            for (int i = 0; i <= 10; i++)
            {
                yield return new()
                {
                    Response = $"Message {i}",
                    StreamingResponseIndex = i,
                };

                await Task.Delay(TimeSpan.FromHours(1)); // Simulate asynchronous work that is stuck after the first request is sent
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> SerialHandlerSingleResponse(IAsyncEnumerable<StreamingExtendedRequest<string>> requestStream, StreamRequestMetadata streamMetadata, ICancelableStreamContext streamContext, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await SaveReceivedRequests(requestStream, streamMetadata, cancellationToken);

            await foreach (var response in GetStringResponseStream(3))
            {
                yield return response;
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> SerialHandlerMultipleResponses(IAsyncEnumerable<StreamingExtendedRequest<string>> requestStream, StreamRequestMetadata streamMetadata, ICancelableStreamContext streamContext, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await SaveReceivedRequests(requestStream, streamMetadata, cancellationToken);

            await foreach (var response in GetStringResponseStream(3))
            {
                _sentResponses.TryAdd(streamMetadata.CorrelationId, new());
                if (_sentResponses.TryGetValue(streamMetadata.CorrelationId, out var sentResponses))
                {
                    sentResponses.Add(response);
                }

                yield return response;
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> SerialHandlerMultipleResponsesWithDelay(IAsyncEnumerable<StreamingExtendedRequest<string>> requestStream, StreamRequestMetadata streamMetadata, ICancelableStreamContext streamContext, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await SaveReceivedRequests(requestStream, streamMetadata, cancellationToken);

            await foreach (var response in GetStringResponseStream(3))
            {
                _sentResponses.TryAdd(streamMetadata.CorrelationId, new());
                if (_sentResponses.TryGetValue(streamMetadata.CorrelationId, out var sentResponses))
                {
                    sentResponses.Add(response);
                }

                yield return response;

                //TODO cancellation token stuff
                await Task.Delay(TimeSpan.FromHours(1));
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> SerialHandlerWithCancellation(IAsyncEnumerable<StreamingExtendedRequest<string>> requestStream, StreamRequestMetadata streamMetadata, ICancelableStreamContext streamContext, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            CancellationTokenSource requestTimeoutCancellationTokenSource = new CancellationTokenSource();
            requestTimeoutCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(1));

            try
            {
                await foreach (var request in requestStream.WithCancellation(requestTimeoutCancellationTokenSource.Token))
                {
                    // TODO check first request?
                    yield return new StreamingExtendedResponse<string>()
                    {
                        Response = request.Request,
                        StreamingResponseIndex = request.StreamingRequestIndex,
                    };
                }
            }
            catch (OperationCanceledException)
            {
                await streamContext.CancelAsync();
                throw;
            }
        }

        private async Task SaveReceivedRequests(IAsyncEnumerable<StreamingExtendedRequest<string>> requestStream, StreamRequestMetadata streamMetadata, CancellationToken cancellationToken)
        {
            await foreach (StreamingExtendedRequest<string> requestStreamEntry in requestStream.WithCancellation(cancellationToken))
            {
                // doesn't overwrite if the correlationId already exists in the dictionary
                _receivedRequests.TryAdd(streamMetadata.CorrelationId, new());

                if (_receivedRequests.TryGetValue(streamMetadata.CorrelationId, out var requestsReceived))
                {
                    requestsReceived.Add(requestStreamEntry);
                }
            }
        }
    }
}
