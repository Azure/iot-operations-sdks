// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Streaming;

namespace StreamingCommandServer
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly MqttSessionClient _mqttClient;
        private readonly StreamingCommandExecutorStub _streamingCommandExecutor;

        public Worker(ILogger<Worker> logger, MqttSessionClient mqttClient)
        {
            _logger = logger;
            _mqttClient = mqttClient;
            _streamingCommandExecutor = new()
            {
                // Can choose to respond to requests as they arrive or after they have all arrived
                OnStreamingCommandReceived = ParallelEchoStreamingCommandHandler
                //OnStreamingCommandReceived = SerialEchoStreamingCommandHandler
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _streamingCommandExecutor.StartAsync(stoppingToken);
        }

        protected ValueTask DisposeAsync() => _streamingCommandExecutor.DisposeAsync();

        // Echos back the request stream entry-by-entry as the request stream is read.
        private async IAsyncEnumerable<StreamingExtendedResponse<string>> ParallelEchoStreamingCommandHandler(IStreamContext<ReceivedStreamingExtendedRequest<string>> streamContext, RequestStreamMetadata metadata)
        {
            _logger.LogInformation("Handling new request stream with correlation Id {correlationId}", metadata.CorrelationId);

            bool streaming = true;
            var asyncEnumerator = streamContext.Entries.WithCancellation(streamContext.CancellationToken).GetAsyncEnumerator();
            while (streaming)
            {
                try
                {
                    streaming = await asyncEnumerator.MoveNextAsync();
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Client-side application cancelled this stream exchange while this side was streaming requests. Will now end the response stream and stop reading the request stream.");
                    Dictionary<string, string>? cancellationRequestUserProperties = streamContext.GetCancellationRequestUserProperties();
                    if (cancellationRequestUserProperties != null)
                    {
                        _logger.LogInformation("Client-side cancellation request included user properties: ");
                        foreach (string key in cancellationRequestUserProperties.Keys)
                        {
                            _logger.LogInformation("Key: {key}, Value: {value}", key, cancellationRequestUserProperties[key]);
                        }
                    }
                    yield break;
                }

                if (!streaming)
                {
                    _logger.LogInformation("Client-side application ended the request stream with an empty request, so this side will end the response stream with an empty response.");
                    yield break;
                }

                var streamedRequest = asyncEnumerator.Current;
                _logger.LogInformation("Received request in request stream with payload {payload} at position {index}", streamedRequest.Payload, streamedRequest.Metadata.Index);

                Dictionary<string, string> userProperties = new Dictionary<string, string>()
                {
                    { "myCustomStreamingResponseKey", "myCustomStreamingResponseValue"}
                };

                yield return new StreamingExtendedResponse<string>(streamedRequest.Payload, new() { UserData = userProperties });
            }
        }

        // Echos back all received requests once the request stream has been read to completion.
        private async IAsyncEnumerable<StreamingExtendedResponse<string>> SerialEchoStreamingCommandHandler(IStreamContext<ReceivedStreamingExtendedRequest<string>> streamContext, RequestStreamMetadata metadata)
        {
            _logger.LogInformation("Handling new request stream with correlation Id {correlationId}", metadata.CorrelationId);

            List<ReceivedStreamingExtendedRequest<string>> receivedRequests = new();
            try
            {
                await foreach (ReceivedStreamingExtendedRequest<string> streamedRequest in streamContext.Entries.WithCancellation(streamContext.CancellationToken))
                {
                    _logger.LogInformation("Received request in request stream with payload {payload} at position {index}", streamedRequest.Payload, streamedRequest.Metadata.Index);
                    receivedRequests.Add(streamedRequest);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Received cancellation request from client-side while streaming requests. Will forgo sending any responses.");
                Dictionary<string, string>? cancellationRequestUserProperties = streamContext.GetCancellationRequestUserProperties();
                if (cancellationRequestUserProperties != null)
                {
                    _logger.LogInformation("Client-side cancellation request included user properties: ");
                    foreach (string key in cancellationRequestUserProperties.Keys)
                    {
                        _logger.LogInformation("Key: {key}, Value: {value}", key, cancellationRequestUserProperties[key]);
                    }
                }
                yield break;
            }

            Dictionary<string, string> userProperties = new Dictionary<string, string>()
            {
                { "myCustomStreamingResponseKey", "myCustomStreamingResponseValue"}
            };

            foreach (ReceivedStreamingExtendedRequest<string> receivedRequest in receivedRequests)
            {
                try
                {
                    // Simulate some asynchronous processing of each request before sending a response
                    await Task.Delay(TimeSpan.FromMilliseconds(10), streamContext.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Received cancellation request from client-side while streaming responses. Will forgo sending any further responses.");
                    Dictionary<string, string>? cancellationRequestUserProperties = streamContext.GetCancellationRequestUserProperties();
                    if (cancellationRequestUserProperties != null)
                    {
                        _logger.LogInformation("Client-side cancellation request included user properties: ");
                        foreach (string key in cancellationRequestUserProperties.Keys)
                        {
                            _logger.LogInformation("Key: {key}, Value: {value}", key, cancellationRequestUserProperties[key]);
                        }
                    }
                    yield break;
                }

                yield return new StreamingExtendedResponse<string>(receivedRequest.Payload, new() { UserData = userProperties });
            }
        }
    }
}
