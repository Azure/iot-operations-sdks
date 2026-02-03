// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Streaming;

namespace Azure.Iot.Operations.Protocol.IntegrationTests
{
    public class StreamingIntegrationTests
    {
        // shared across all tests in this file, but each test should use a unique GUID as the correlationId of the stream
        private readonly ConcurrentDictionary<Guid, List<StreamingExtendedRequest<string>>> _receivedRequests = new();
        private readonly ConcurrentDictionary<Guid, List<StreamingExtendedResponse<string>>> _sentResponses = new();

#pragma warning disable CS9113 // Parameter is unread.
#pragma warning disable IDE0060 // Remove unused parameter
        internal class StringStreamingCommandInvoker(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            : StreamingCommandInvoker<string, string>()
        { }

        internal class EchoStringStreamingCommandExecutor : StreamingCommandExecutor<string, string>
        {
            internal EchoStringStreamingCommandExecutor(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, string commandName = "echo")
#pragma warning restore IDE0060 // Remove unused parameter
                : base()
#pragma warning restore CS9113 // Parameter is unread.

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

            RequestStreamMetadata requestMetadata = new();
            var responseStreamContext = await invoker.InvokeStreamingCommandAsync(GetStringRequestStream(requestCount), requestMetadata);

            List<StreamingExtendedResponse<string>> receivedResponses = new();
            await foreach (StreamingExtendedResponse<string> response in responseStreamContext.Entries)
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
                Assert.Equal(expectedRequests[i].Payload, receivedRequests[i].Payload);
                Assert.Equal(i, receivedRequests[i].Metadata!.Index);
            }

            if (!_sentResponses.TryGetValue(requestMetadata.CorrelationId, out var sentResponses))
            {
                Assert.Fail("Executor did not send any responses");
            }

            Assert.Equal(receivedResponses.Count, sentResponses.Count);
            for (int i = 0; i < expectedRequests.Count; i++)
            {
                Assert.Equal(sentResponses[i].Payload, receivedResponses[i].Payload);
                Assert.Equal(i, receivedResponses[i].Metadata!.Index);
            }
        }

        //TODO add user properties to these tests
        [Fact]
        public async Task InvokerCanCancelWhileStreamingRequests() //TODO does cancellation token trigger on executor side? Add to other tests as well
        {
            await using MqttSessionClient invokerMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient executorMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();

            await using EchoStringStreamingCommandExecutor executor =
                new(new(), executorMqttClient)
                {
                    OnStreamingCommandReceived = SerialHandlerExpectsCancellationWhileStreamingRequests
                };

            await executor.StartAsync();

            await using StringStreamingCommandInvoker invoker = new(new(), invokerMqttClient);

            var stream = await invoker.InvokeStreamingCommandAsync(GetStringRequestStreamWithDelay());

            Dictionary<string, string> cancellationCustomUserProperties = new()
            {
                { "someUserPropertyKey", "someUserPropertyValue"}
            };

            await stream.CancelAsync(cancellationCustomUserProperties);

            //TODO assert the executor received cancellation + user properties
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

            var responseStreamContext = await invoker.InvokeStreamingCommandAsync(GetStringRequestStream(1));

            await foreach (var response in responseStreamContext.Entries)
            {
                Dictionary<string, string> cancellationCustomUserProperties = new()
                {
                    { "someUserPropertyKey", "someUserPropertyValue"}
                };

                await responseStreamContext.CancelAsync(cancellationCustomUserProperties);
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
                    OnStreamingCommandReceived = SerialHandlerThatCancelsWhileStreamingRequests
                };

            await executor.StartAsync();

            await using StringStreamingCommandInvoker invoker = new(new(), invokerMqttClient);

            var responseStreamContext = await invoker.InvokeStreamingCommandAsync(GetStringRequestStreamWithDelay());

            bool receivedCancellation = false;
            try
            {
                await foreach (var response in responseStreamContext.Entries)
                {
                    // Executor should send cancellation request prior to sending any responses
                }
            }
            catch (AkriMqttException ame) when (ame.Kind is AkriMqttErrorKind.Cancellation)
            {
                receivedCancellation = true;
                Assert.True(responseStreamContext.CancellationToken.IsCancellationRequested); // TODO timing on exception thrown vs cancellation token triggered?
                Dictionary<string, string>? cancellationRequestUserProperties = responseStreamContext.GetCancellationRequestUserProperties();
                Assert.NotNull(cancellationRequestUserProperties);
                Assert.NotEmpty(cancellationRequestUserProperties.Keys); //TODO actually validate the values match
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
                    OnStreamingCommandReceived = SerialHandlerThatCancelsStreamingWhileStreamingResponses
                };

            await executor.StartAsync();

            await using StringStreamingCommandInvoker invoker = new(new(), invokerMqttClient);

            var responseStreamContext = await invoker.InvokeStreamingCommandAsync(GetStringRequestStream(1));

            bool receivedCancellation = false;
            try
            {
                await foreach (var response in responseStreamContext.Entries)
                {
                    // Read responses until the executor sends a cancellation request
                }
            }
            catch (AkriMqttException ame) when (ame.Kind is AkriMqttErrorKind.Cancellation)
            {
                receivedCancellation = true;
                Assert.True(responseStreamContext.CancellationToken.IsCancellationRequested); // TODO timing on exception thrown vs cancellation token triggered?
                Dictionary<string, string>? cancellationRequestUserProperties = responseStreamContext.GetCancellationRequestUserProperties();
                Assert.NotNull(cancellationRequestUserProperties);
                Assert.NotEmpty(cancellationRequestUserProperties.Keys); //TODO actually validate the values match
            }

            Assert.True(receivedCancellation);
        }

        // Can configure the executor to send a response for each request and the invoker to only send the nth request after receiving the n-1th response
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

            RequestStreamMetadata requestMetadata = new();
            TaskCompletionSource tcs1 = new(); // the delay to impose before sending the first request in the request stream
            TaskCompletionSource tcs2 = new(); // the delay to impose before sending the second request in the request stream
            TaskCompletionSource tcs3 = new(); // the delay to impose before sending the third request in the request stream

            tcs1.TrySetResult(); // Don't need to delay the first message

            var responseStreamContext = await invoker.InvokeStreamingCommandAsync(GetStringRequestStreamWithDelay(tcs1, tcs2, tcs3), requestMetadata);

            List<StreamingExtendedResponse<string>> receivedResponses = new();
            await foreach (StreamingExtendedResponse<string> response in responseStreamContext.Entries)
            {
                receivedResponses.Add(response);

                //TOOD metadata will never be null when received, but may be null when assigned
                if (response.Metadata!.Index == 0)
                {
                    // The first response has been received, so allow the second request to be sent
                    tcs2.TrySetResult();
                }

                if (response.Metadata!.Index == 1)
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
                Assert.Equal(receivedResponses[i].Payload, receivedRequests[i].Payload);
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

        // In cases where the IAsyncEnumerable isn't sure if a given entry will be the last, users can "escape" by using the keyword
        // "yield break" to signal the IAsyncEnumerable has ended without providing a fully-fledged final entry
        [Fact]
        public async Task InvokerCanCompleteRequestStreamWithYieldBreak()
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

            var stream = await invoker.InvokeStreamingCommandAsync(GetStringRequestStreamWithYieldBreak());

            await foreach (var response in stream.Entries)
            {
                // TODO verify expected responses
            }
        }

        // See 'InvokerCanCompleteRequestStreamWithYieldBreak' but on the executor side
        [Fact]
        public async Task ExecutorCanCompleteResponseStreamWithYieldBreak()
        {
            await using MqttSessionClient invokerMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient executorMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();

            await using EchoStringStreamingCommandExecutor executor =
                new(new(), executorMqttClient)
                {
                    OnStreamingCommandReceived = SerialHandlerMultipleResponsesWithYieldBreakAfterFirstResponse
                };

            await executor.StartAsync();

            await using StringStreamingCommandInvoker invoker = new(new(), invokerMqttClient);

            var stream = await invoker.InvokeStreamingCommandAsync(GetStringRequestStream(3));

            await foreach (var response in stream.Entries)
            {
                // TODO verify expected responses
            }
        }

        [Fact]
        public async Task InvokerAndExecutorCanDelayAcknowledgements()
        {
            await using MqttSessionClient invokerMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient executorMqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();

            await using EchoStringStreamingCommandExecutor executor =
                new(new(), executorMqttClient)
                {
                    OnStreamingCommandReceived = SerialHandlerSingleResponseManualAcks
                };

            executor.AutomaticallyAcknowledgeRequests = false;

            await executor.StartAsync();

            await using StringStreamingCommandInvoker invoker = new(new(), invokerMqttClient);

            invoker.AutomaticallyAcknowledgeResponses = false;

            var stream = await invoker.InvokeStreamingCommandAsync(GetStringRequestStream(3));

            await foreach (var response in stream.Entries)
            {
                await response.AcknowledgeAsync();
            }
        }

        private async IAsyncEnumerable<StreamingExtendedRequest<string>> GetStringRequestStream(int requestCount)
        {
            for (int i = 0; i < requestCount; i++)
            {
                await Task.Delay(TimeSpan.FromMicroseconds(1)); // Simulate asynchronous work
                yield return new($"Message {i}");
            }
        }

        // send N requests after each provided TCS is triggered. This allows for testing scenarios like "only send a request once a response has been received"
        private async IAsyncEnumerable<StreamingExtendedRequest<string>> GetStringRequestStreamWithDelay(params TaskCompletionSource[] delays)
        {
            int index = 0;
            foreach (TaskCompletionSource delay in delays)
            {
                await delay.Task; // Simulate asynchronous work
                yield return new($"Message {index++}");
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> GetStringStreamContext(int responseCount)
        {
            for (int i = 0; i < responseCount; i++)
            {
                await Task.Delay(TimeSpan.FromMicroseconds(1)); // Simulate asynchronous work
                yield return new($"Message {i}");
            }
        }

        private async IAsyncEnumerable<StreamingExtendedRequest<string>> GetStringRequestStreamWithDelay()
        {
            for (int i = 0; i <= 10; i++)
            {
                yield return new($"Message {i}");

                await Task.Delay(TimeSpan.FromHours(1)); // Simulate asynchronous work that is stuck after the first request is sent
            }
        }

        // Simulate a request stream that decides between entries to close gracefully
        private static async IAsyncEnumerable<StreamingExtendedRequest<string>> GetStringRequestStreamWithYieldBreak()
        {
            for (int i = 0; true; i++)
            {
                await Task.Delay(TimeSpan.FromMicroseconds(1)); // Simulate asynchronous work

                if (i > 5)
                {
                    yield break;
                }

                yield return new($"Message {i}");
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> SerialHandlerSingleResponse(IStreamContext<ReceivedStreamingExtendedRequest<string>> stream, RequestStreamMetadata streamMetadata)
        {
            await SaveReceivedRequests(stream, streamMetadata, false, stream.CancellationToken);

            await foreach (var response in GetStringStreamContext(3).WithCancellation(stream.CancellationToken))
            {
                yield return response;
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> SerialHandlerExpectsCancellationWhileStreamingRequests(IStreamContext<ReceivedStreamingExtendedRequest<string>> stream, RequestStreamMetadata streamMetadata)
        {
            try
            {
                await foreach (var request in stream.Entries.WithCancellation(stream.CancellationToken))
                {
                }
            }
            catch (OperationCanceledException)
            {
                // The stream was cancelled by the invoker while it streamed requests
                Dictionary<string, string>? cancellationUserProperties = stream.GetCancellationRequestUserProperties();

                //TODO assert received user properties in the cancellation request
            }

            yield return new("should never be reached");
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> SerialHandlerMultipleResponses(IStreamContext<ReceivedStreamingExtendedRequest<string>> stream, RequestStreamMetadata streamMetadata)
        {
            await SaveReceivedRequests(stream, streamMetadata, false, stream.CancellationToken);

            await foreach (var response in GetStringStreamContext(3).WithCancellation(stream.CancellationToken))
            {
                _sentResponses.TryAdd(streamMetadata.CorrelationId, new());
                if (_sentResponses.TryGetValue(streamMetadata.CorrelationId, out var sentResponses))
                {
                    sentResponses.Add(response);
                }

                yield return response;
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> SerialHandlerMultipleResponsesWithYieldBreakAfterFirstResponse(IStreamContext<ReceivedStreamingExtendedRequest<string>> stream, RequestStreamMetadata streamMetadata)
        {
            await SaveReceivedRequests(stream, streamMetadata, false, stream.CancellationToken);

            await foreach (var response in GetStringStreamContext(3).WithCancellation(stream.CancellationToken))
            {
                _sentResponses.TryAdd(streamMetadata.CorrelationId, new());
                if (_sentResponses.TryGetValue(streamMetadata.CorrelationId, out var sentResponses))
                {
                    sentResponses.Add(response);
                }

                yield return response;
                yield break; // Break after sending the first response
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> ParallelHandlerEchoResponses(IStreamContext<ReceivedStreamingExtendedRequest<string>> stream, RequestStreamMetadata streamMetadata)
        {
            await foreach (StreamingExtendedRequest<string> requestStreamEntry in stream.Entries.WithCancellation(stream.CancellationToken))
            {
                // doesn't overwrite if the correlationId already exists in the dictionary
                _receivedRequests.TryAdd(streamMetadata.CorrelationId, new());

                if (_receivedRequests.TryGetValue(streamMetadata.CorrelationId, out var requestsReceived))
                {
                    requestsReceived.Add(requestStreamEntry);
                }

                yield return new(requestStreamEntry.Payload);
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> SerialHandlerMultipleResponsesWithDelay(IStreamContext<ReceivedStreamingExtendedRequest<string>> stream, RequestStreamMetadata streamMetadata)
        {
            await SaveReceivedRequests(stream, streamMetadata, false, stream.CancellationToken);

            var asyncEnumeratorWithCancellation = GetStringRequestStreamWithDelay().WithCancellation(stream.CancellationToken).GetAsyncEnumerator();

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
                    // The invoker side will cancel this stream of responses (via the provided cancellation token) since it takes too long

                    Dictionary<string, string>? cancellationUserProperties = stream.GetCancellationRequestUserProperties();

                    //TODO assert these match the user properties sent by the invoker

                    yield break;
                }

                yield return new(request.Payload);
            }
        }

        private static async IAsyncEnumerable<StreamingExtendedResponse<string>> SerialHandlerThatCancelsWhileStreamingRequests(IStreamContext<ReceivedStreamingExtendedRequest<string>> stream, RequestStreamMetadata streamMetadata)
        {
            CancellationTokenSource requestTimeoutCancellationTokenSource = new CancellationTokenSource();
            requestTimeoutCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(1));

            var asyncEnumeratorWithCancellation = stream.Entries.WithCancellation(requestTimeoutCancellationTokenSource.Token).GetAsyncEnumerator();

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

                yield return new(request.Payload);
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> SerialHandlerThatCancelsStreamingWhileStreamingResponses(IStreamContext<ReceivedStreamingExtendedRequest<string>> stream, RequestStreamMetadata streamMetadata)
        {
            await SaveReceivedRequests(stream, streamMetadata, false, stream.CancellationToken);

            CancellationTokenSource cts = new();
            cts.CancelAfter(TimeSpan.FromSeconds(1));
            for (int responseCount = 0; responseCount < 5; responseCount++)
            {
                if (responseCount == 3)
                {
                    Dictionary<string, string> cancellationCustomUserProperties = new()
                    {
                        { "someUserPropertyKey", "someUserPropertyValue"}
                    };

                    await stream.CancelAsync(cancellationCustomUserProperties);
                    yield break;
                }

                yield return new StreamingExtendedResponse<string>("some response");
            }
        }

        private async IAsyncEnumerable<StreamingExtendedResponse<string>> SerialHandlerSingleResponseManualAcks(IStreamContext<ReceivedStreamingExtendedRequest<string>> stream, RequestStreamMetadata streamMetadata)
        {
            await SaveReceivedRequests(stream, streamMetadata, true, stream.CancellationToken);

            await foreach (var response in GetStringStreamContext(3).WithCancellation(stream.CancellationToken))
            {
                yield return response;
            }
        }

        private async Task SaveReceivedRequests(IStreamContext<ReceivedStreamingExtendedRequest<string>> stream, RequestStreamMetadata streamMetadata, bool manualAcks, CancellationToken cancellationToken)
        {
            await foreach (ReceivedStreamingExtendedRequest<string> requestStreamEntry in stream.Entries.WithCancellation(cancellationToken))
            {
                // doesn't overwrite if the correlationId already exists in the dictionary
                _receivedRequests.TryAdd(streamMetadata.CorrelationId, new());

                if (_receivedRequests.TryGetValue(streamMetadata.CorrelationId, out var requestsReceived))
                {
                    requestsReceived.Add(requestStreamEntry);
                }

                if (manualAcks)
                {
                    await requestStreamEntry.AcknowledgeAsync();
                }
            }
        }
    }
}
