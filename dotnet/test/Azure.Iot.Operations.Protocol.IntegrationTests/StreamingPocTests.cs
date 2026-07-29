// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Streaming;
using TestEnvoys;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

// Minimal end-to-end POC: a request stream of N entries echoed 1:1 as a response stream.
public class StreamingPocTests
{
    [CommandTopic("rpc/streaming/poc/echo")]
    private sealed class EchoInvoker(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandInvoker<string, string>(appContext, mqttClient, "echo", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/echo")]
    private sealed class EchoExecutor(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandExecutor<string, string>(appContext, mqttClient, "echo", new Utf8JsonSerializer());

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task EchoStreamRoundTrips(int count)
    {
        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        await using EchoExecutor executor = new(appContext, executorClient)
        {
            OnStreamingCommandReceived = EchoHandler,
        };
        await executor.StartAsync();

        await using EchoInvoker invoker = new(appContext, invokerClient);

        var (responses, _) = await invoker.InvokeStreamingCommandAsync(
            RequestStream(count),
            new RequestStreamMetadata { CorrelationId = Guid.NewGuid() });

        List<string> received = new();
        await foreach (StreamingExtendedResponse<string> response in responses.Entries)
        {
            received.Add(response.Payload);
        }

        Assert.Equal(count, received.Count);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal($"echo: Message {i}", received[i]);
        }
    }

    // 1:1 echo: each request entry becomes one response entry.
    private static (IAsyncEnumerable<StreamingExtendedResponse<string>> Responses, ResponseStreamMetadata Metadata) EchoHandler(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests,
        RequestStreamMetadata requestMetadata,
        IExchangeContext exchange) =>
        (EchoResponses(requests), new ResponseStreamMetadata());

    private static async IAsyncEnumerable<StreamingExtendedResponse<string>> EchoResponses(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests)
    {
        await foreach (ReceivedStreamingExtendedRequest<string> request in requests.Entries)
        {
            yield return new StreamingExtendedResponse<string>($"echo: {request.Payload}");
        }
    }

    private static async IAsyncEnumerable<StreamingExtendedRequest<string>> RequestStream(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await Task.Yield();
            yield return new StreamingExtendedRequest<string>($"Message {i}");
        }
    }
}
