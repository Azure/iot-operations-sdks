// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Streaming;

namespace StreamingCommandClient
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly MqttSessionClient _mqttClient;
        private readonly StreamingCommandInvokerStub _streamingCommandInvoker;

        public Worker(ILogger<Worker> logger, MqttSessionClient mqttClient)
        {
            _logger = logger;
            _mqttClient = mqttClient;
            _streamingCommandInvoker = new StreamingCommandInvokerStub();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var streamContext = await _streamingCommandInvoker.InvokeStreamingCommandAsync(GetRequestStream(stoppingToken));

                // This streaming exchange can be cancelled either because this client-side application is shutting down or because
                // the service side requested cancellation
                using var unifiedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, streamContext.CancellationToken);

                try
                {
                    await foreach (var streamResponse in streamContext.Entries.WithCancellation(unifiedCancellationTokenSource.Token))
                    {
                        _logger.LogInformation("Received response in response stream with payload {payload} at position {index}", streamResponse.Payload, streamResponse.Metadata.Index);
                    }
                }
                catch (OperationCanceledException)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Client-side application shutting down. Sending cancellation request to service-side application.");
                        await streamContext.CancelAsync();
                        _logger.LogInformation("Service-side application has successfully cancelled this stream exchange.");
                    }
                    else
                    {
                        _logger.LogInformation("Service side application sent cancellation request from service-side, so no further responses will be read.");
                        Dictionary<string, string>? cancellationRequestUserProperties = streamContext.GetCancellationRequestUserProperties();
                        if (cancellationRequestUserProperties != null)
                        {
                            _logger.LogInformation("Service-side cancellation request included user properties: ");
                            foreach (string key in cancellationRequestUserProperties.Keys)
                            {
                                _logger.LogInformation("Key: {key}, Value: {value}", key, cancellationRequestUserProperties[key]);
                            }
                        }
                    }
                }

                _logger.LogInformation("Completed the streaming exchange with the streaming command service. Waiting a bit before starting a new stream exchange");

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        private static async IAsyncEnumerable<StreamingExtendedRequest<string>> GetRequestStream([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Dictionary<string, string> userProperties = new Dictionary<string, string>()
            {
                { "myCustomStreamingRequestKey", "myCustomStreamingRequestValue"}
            };

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken); // simulate asynchronous work
                }
                catch (OperationCanceledException)
                {
                    // Application is shutting down, so stop yielding new entries in the request stream
                    yield break;
                }

                yield return new("some payload", new() { UserData = userProperties });
            }
        }
    }
}
