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

            await using EchoStringStreamingCommandExecutor executor = multipleResponses
                ? new(new(), executorMqttClient)
                 {
                     OnStreamingCommandReceived = SerialHandlerMultipleResponses
                 }
                : new(new(), executorMqttClient)
                {
                    OnStreamingCommandReceived = SerialHandlerSingleResponse
                };

            await executor.StartAsync();

            await using StringStreamingCommandInvoker invoker = new(new(), invokerMqttClient);

            StreamRequestMetadata requestMetadata = new();
            var streamContext = await invoker.InvokeStreamingCommandAsync(GetStringRequestStream(requestCount), requestMetadata);

            List<StreamingExtendedResponse<string>> receivedResponses = new();
            await foreach (StreamingExtendedResponse<string> response in streamContext.Responses)
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
        public async Task InvokerCanCancelWhileStreamingRequests() //TODO does cancellation token trigger on executor side? Add to other tests as well
        {
            await using MqttSessionClient invokerMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient executorMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();

            await using EchoStringStreamingCommandExecutor executor =
                new(new(), executorMqttClient)
                {
                    OnStreamingCommandReceived = SerialHandlerSingleResponse
                };

            await executor.StartAsync();

            await using StringStreamingCommandInvoker invoker = new(new(), invokerMqttClient);

            StreamRequestMetadata requestMetadata = new();
            var stream = await invoker.InvokeStreamingCommandAsync(GetStringRequestStreamWithDelay(), requestMetadata);

            await stream.CancelAsync();
        }

        [Fact]
        public async Task InvokerCanCancelWhileStreamingResponses()
        {
            await using MqttSessionClient invokerMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient executorMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();

            await using EchoStringStreamingCommandExecutor executor = new(new(), executorMqttClient)
            {
                OnStreamingCommandReceived = SerialHandlerMultipleResponsesWithDelay
            };

            await executor.StartAsync();

            await using StringStreamingCommandInvoker invoker = new(new(), invokerMqttClient);

            StreamRequestMetadata requestMetadata = new();
            var streamContext = await invoker.InvokeStreamingCommandAsync(GetStringRequestStream(1), requestMetadata);

            await foreach (var response in streamContext.Responses)
            {
                //TODO check first response?

                //TODO how to check that the tear down happened on executor side? something like saving the state via guid like with saving requests/responses?
                await streamContext.CancelAsync();
                break;
            }
        }

        [Fact]
        public async Task ExecutorCanCancelWhileStreamingRequests()
        {
            await using MqttSessionClient invokerMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient executorMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();

            await using EchoStringStreamingCommandExecutor executor =
                new(new(), executorMqttClient)
                {
                    OnStreamingCommandReceived = SerialHandlerWithCancellationWhileStreamingRequests
                };

            await executor.StartAsync();

            await using StringStreamingCommandInvoker invoker = new(new(), invokerMqttClient);

            StreamRequestMetadata requestMetadata = new();
            var streamContext = await invoker.InvokeStreamingCommandAsync(GetStringRequestStreamWithDelay(), requestMetadata);

            bool receivedCancellation = false;
            try
            {
                await foreach (var response in streamContext.Responses)
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
        public async Task ExecutorCanCancelWhileStreamingResponses()
        {
            await using MqttSessionClient invokerMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient executorMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();

            await using EchoStringStreamingCommandExecutor executor =
                new(new(), executorMqttClient)
                {
                    OnStreamingCommandReceived = SerialHandlerWithCancellationWhileStreamingResponses
                };

            await executor.StartAsync();

            await using StringStreamingCommandInvoker invoker = new(new(), invokerMqttClient);

            StreamRequestMetadata requestMetadata = new();
            var streamContext = await invoker.InvokeStreamingCommandAsync(GetStringRequestStream(1), requestMetadata);

            bool receivedCancellation = false;
            try
            {
                await foreach (var response in streamContext.Responses)
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
        public async Task CanStreamRequestsAndResponsesSimultaneously()
        {
            await using MqttSessionClient invokerMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient executorMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();

            await using EchoStringStreamingCommandExecutor executor = new(new(), executorMqttClient)
            {
                OnStreamingCommandReceived = ParallelHandlerEchoResponses
            };

            await executor.StartAsync();

            await using StringStreamingCommandInvoker invoker = new(new(), invokerMqttClient);

            StreamRequestMetadata requestMetadata = new();
            TaskCompletionSource tcs1 = new(); // the delay to impose before sending the first request in the request stream
            TaskCompletionSource tcs2 = new(); // the delay to impose before sending the second request in the request stream
            TaskCompletionSource tcs3 = new(); // the delay to impose before sending the third request in the request stream

            tcs1.TrySetResult(); // Don't need to delay the first message

            var streamContext = await invoker.InvokeStreamingCommandAsync(GetStringRequestStreamWithDelay(tcs1, tcs2, tcs3), requestMetadata);

            List<StreamingExtendedResponse<string>> receivedResponses = new();
            await foreach (StreamingExtendedResponse<string> response in streamContext.Responses)
            {
                receivedResponses.Add(response);

                if (response.StreamingResponseIndex == 0)
                {
                    // The first response has been received, so allow the second request to be sent
                    tcs2.TrySetResult();
                }

                if (response.StreamingResponseIndex == 1)
                {
                    // The second response has been received, so allow the third request to be sent
                    tcs2.TrySetResult();
                }
            }

            if (!_receivedRequests.TryGetValue(requestMetadata.CorrelationId, out var receivedRequests))
            {
                Assert.Fail("Executor did not receive any requests");
            }

            // Executor should echo back each request as a response
            Assert.Equal(receivedResponses.Count, receivedRequests.Count);
            for (int i = 0; i < receivedResponses.Count; i++)
            {
                Assert.Equal(receivedResponses[i].Response, receivedRequests[i].Request);
            }
        }

        [Fact]
        public Task CanAddUserPropertiesToSpecificToMessagesInRequestAndstreamContexts()
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

        // send N requests after each provided TCS is triggered. This allows for testing scenarios like "only send a request once a response has been received"
        private async IAsyncEnumerable<StreamingExtendedRequest<string>> GetStringRequestStreamWithDelay(params TaskCompletionSource[] delays)
        {
            int index = 0;
            foreach (TaskCompletionSource delay in delays)
            {
                await delay.Task; // Simulate asynchronous work
                yield return new()
                {
                    Request = $"Message {index}",
                    StreamingRequestIndex = index,
                };
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> GetStringStreamContext(int responseCount)
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

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> SerialHandlerSingleResponse(ICancelableRequestStreamContext<string> stream, StreamRequestMetadata streamMetadata, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await SaveReceivedRequests(stream, streamMetadata, cancellationToken);

            await foreach (var response in GetStringStreamContext(3).WithCancellation(cancellationToken))
            {
                yield return response;
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> SerialHandlerMultipleResponses(ICancelableRequestStreamContext<string> stream, StreamRequestMetadata streamMetadata, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await SaveReceivedRequests(stream, streamMetadata, cancellationToken);

            await foreach (var response in GetStringStreamContext(3).WithCancellation(cancellationToken))
            {
                _sentResponses.TryAdd(streamMetadata.CorrelationId, new());
                if (_sentResponses.TryGetValue(streamMetadata.CorrelationId, out var sentResponses))
                {
                    sentResponses.Add(response);
                }

                yield return response;
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> ParallelHandlerEchoResponses(ICancelableRequestStreamContext<string> stream, StreamRequestMetadata streamMetadata, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (StreamingExtendedRequest<string> requestStreamEntry in stream.Requests.WithCancellation(cancellationToken))
            {
                // doesn't overwrite if the correlationId already exists in the dictionary
                _receivedRequests.TryAdd(streamMetadata.CorrelationId, new());

                if (_receivedRequests.TryGetValue(streamMetadata.CorrelationId, out var requestsReceived))
                {
                    requestsReceived.Add(requestStreamEntry);
                }

                yield return new StreamingExtendedResponse<string>()
                {
                    Response = requestStreamEntry.Request,
                    StreamingResponseIndex = requestStreamEntry.StreamingRequestIndex,
                };
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> SerialHandlerMultipleResponsesWithDelay(ICancelableRequestStreamContext<string> stream, StreamRequestMetadata streamMetadata, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await SaveReceivedRequests(stream, streamMetadata, cancellationToken);

            await foreach (var response in GetStringStreamContext(3).WithCancellation(cancellationToken))
            {
                _sentResponses.TryAdd(streamMetadata.CorrelationId, new());
                if (_sentResponses.TryGetValue(streamMetadata.CorrelationId, out var sentResponses))
                {
                    sentResponses.Add(response);
                }

                yield return response;

                await Task.Delay(TimeSpan.FromHours(1), cancellationToken);
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> SerialHandlerWithCancellationWhileStreamingRequests(ICancelableRequestStreamContext<string> stream, StreamRequestMetadata streamMetadata, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            CancellationTokenSource requestTimeoutCancellationTokenSource = new CancellationTokenSource();
            requestTimeoutCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(1));

            var asyncEnumeratorWithCancellation = stream.Requests.WithCancellation(requestTimeoutCancellationTokenSource.Token).GetAsyncEnumerator();

            bool readingRequestStream = true;
            while (readingRequestStream)
            {
                StreamingExtendedRequest<string> request;
                try
                {
                    readingRequestStream = await asyncEnumeratorWithCancellation.MoveNextAsync();
                    request = asyncEnumeratorWithCancellation.Current;
                }
                catch (OperationCanceledException)
                {
                    // simulates timing out while waiting on an entry in the stream and the executor deciding to cancel the stream as a result
                    await stream.CancelAsync();
                    yield break;
                }

                yield return new StreamingExtendedResponse<string>()
                {
                    Response = request.Request,
                    StreamingResponseIndex = request.StreamingRequestIndex,
                };
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> SerialHandlerWithCancellationWhileStreamingResponses(ICancelableRequestStreamContext<string> stream, StreamRequestMetadata streamMetadata, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await SaveReceivedRequests(stream, streamMetadata, cancellationToken);

            CancellationTokenSource cts = new();
            cts.CancelAfter(TimeSpan.FromSeconds(1));
            for (int responseCount = 0; responseCount < 5; responseCount++)
            {
                try
                {
                    if (responseCount == 3)
                    {
                        // simulate one entry in the response stream taking too long and the executor deciding to cancel the stream because of it
                        await Task.Delay(TimeSpan.FromHours(1), cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    await stream.CancelAsync();
                    yield break;
                }

                yield return new StreamingExtendedResponse<string>()
                {
                    Response = "some response",
                    StreamingResponseIndex = responseCount,
                };
            }
        }

        private async Task SaveReceivedRequests(ICancelableRequestStreamContext<string> stream, StreamRequestMetadata streamMetadata, CancellationToken cancellationToken)
        {
            await foreach (StreamingExtendedRequest<string> requestStreamEntry in stream.Requests.WithCancellation(cancellationToken))
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
