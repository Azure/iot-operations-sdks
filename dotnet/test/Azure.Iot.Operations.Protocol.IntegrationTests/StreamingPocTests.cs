// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Streaming;
using TestEnvoys;
using Xunit.Abstractions;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

// Minimal end-to-end POC for streaming RPC: ping-pong (1:1, immediate) and sort-array (buffer-all).
public class StreamingPocTests(ITestOutputHelper output)
{
    [CommandTopic("rpc/streaming/poc/asyncpingpong")]
    private sealed class AsyncPingPongInvoker(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandInvoker<string, string>(appContext, mqttClient, "asyncpingpong", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/asyncpingpong")]
    private sealed class AsyncPingPongExecutor(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandExecutor<string, string>(appContext, mqttClient, "asyncpingpong", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/syncpingpong")]
    private sealed class SyncPingPongInvoker(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandInvoker<string, string>(appContext, mqttClient, "syncpingpong", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/syncpingpong")]
    private sealed class SyncPingPongExecutor(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandExecutor<string, string>(appContext, mqttClient, "syncpingpong", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/sort")]
    private sealed class SortInvoker(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandInvoker<string, string>(appContext, mqttClient, "sort", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/sort")]
    private sealed class SortExecutor(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandExecutor<string, string>(appContext, mqttClient, "sort", new Utf8JsonSerializer());

    private void Log(string message) =>
        output.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  {message}");

    // Frames a scenario line with separators so it stands out in the captured log.
    private void LogScenario(string message)
    {
        Log(new string('=', 80));
        Log(message);
        Log(new string('=', 80));
    }

    // Async ping-pong: each "ping i" request is answered with "pong i" immediately as it arrives.
    [Theory]
    [InlineData(10)]
    public async Task AsyncPingPong(int count)
    {
        LogScenario($"SCENARIO async ping-pong: invoker fires all {count} pings up front; executor echoes each as a pong; responses stream back concurrently.");
        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        await using AsyncPingPongExecutor executor = new(appContext, executorClient)
        {
            OnStreamingCommandReceived = PingPongHandler,
            Log = Log,
        };
        await executor.StartAsync();

        await using AsyncPingPongInvoker invoker = new(appContext, invokerClient)
        {
            Log = Log,
        };

        Guid correlationId = Guid.NewGuid();
        Log($"invoking async ping-pong: {count} ping(s), correlationId={correlationId}");

        var (responses, _) = await invoker.InvokeStreamingCommandAsync(
            AsyncPingStream(count),
            new RequestStreamMetadata { CorrelationId = correlationId });

        List<string> received = new();
        await foreach (StreamingExtendedResponse<string> response in responses.Entries)
        {
            received.Add(response.Payload);
        }

        Log($"exchange complete: invoker received {received.Count} pong(s)");

        Assert.Equal(count, received.Count);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal($"pong {i}", received[i]);
        }
    }

    private (IAsyncEnumerable<StreamingExtendedResponse<string>> Responses, ResponseStreamMetadata Metadata) PingPongHandler(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests,
        RequestStreamMetadata requestMetadata,
        IExchangeContext exchange) =>
        (PongResponses(requests), new ResponseStreamMetadata());

    private async IAsyncEnumerable<StreamingExtendedResponse<string>> PongResponses(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests)
    {
        await foreach (ReceivedStreamingExtendedRequest<string> request in requests.Entries)
        {
            yield return new StreamingExtendedResponse<string>(request.Payload.Replace("ping", "pong"));
        }
    }

    private async IAsyncEnumerable<StreamingExtendedRequest<string>> AsyncPingStream(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await Task.Yield();
            yield return new StreamingExtendedRequest<string>($"ping {i}");
        }
    }

    // Synchronous ping-pong: the invoker waits for each "pong i" before sending the next "ping i+1",
    // so only one entry is ever in flight. Same executor as ping-pong; the pacing is entirely invoker-side.
    [Theory]
    [InlineData(10)]
    public async Task SyncPingPong(int count)
    {
        LogScenario($"SCENARIO sync ping-pong: invoker waits for each pong before sending the next of {count} pings; only one entry is in flight at a time.");
        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        await using SyncPingPongExecutor executor = new(appContext, executorClient)
        {
            OnStreamingCommandReceived = PingPongHandler,
            Log = Log,
        };
        await executor.StartAsync();

        await using SyncPingPongInvoker invoker = new(appContext, invokerClient)
        {
            Log = Log,
        };

        // Released once per received pong; gates the next ping so only one entry is in flight at a time.
        using SemaphoreSlim pongReceived = new(0);

        Guid correlationId = Guid.NewGuid();
        Log($"invoking sync ping-pong: {count} ping(s), correlationId={correlationId}");

        var (responses, _) = await invoker.InvokeStreamingCommandAsync(
            SyncPingStream(count, pongReceived),
            new RequestStreamMetadata { CorrelationId = correlationId });

        List<string> received = new();
        await foreach (StreamingExtendedResponse<string> response in responses.Entries)
        {
            received.Add(response.Payload);
            pongReceived.Release();
        }

        Log($"exchange complete: invoker received {received.Count} pong(s)");

        Assert.Equal(count, received.Count);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal($"pong {i}", received[i]);
        }
    }

    // Yields "ping i", then blocks until its "pong i" has been received before yielding the next ping.
    private async IAsyncEnumerable<StreamingExtendedRequest<string>> SyncPingStream(int count, SemaphoreSlim pongReceived)
    {
        for (int i = 0; i < count; i++)
        {
            Log($"invoker: sending ping {i}, will wait for its pong before the next");
            yield return new StreamingExtendedRequest<string>($"ping {i}");
            await pongReceived.WaitAsync();
        }
    }

    // Sort-array: invoker streams N random numbers (one per entry); executor buffers the whole
    // request stream, sorts it, then streams the sorted sequence back (one number per entry).
    [Theory]
    [InlineData(10)]
    public async Task SortArray(int count)
    {
        LogScenario($"SCENARIO sort-array: invoker streams {count} random numbers; executor buffers the whole request stream, sorts it, then streams the sorted sequence back.");
        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        await using SortExecutor executor = new(appContext, executorClient)
        {
            OnStreamingCommandReceived = SortHandler,
            Log = Log,
        };
        await executor.StartAsync();

        await using SortInvoker invoker = new(appContext, invokerClient)
        {
            Log = Log,
        };

        Random rng = new();
        int[] numbers = Enumerable.Range(0, count).Select(_ => rng.Next(0, 100)).ToArray();
        int[] expected = numbers.OrderBy(n => n).ToArray();

        Guid correlationId = Guid.NewGuid();
        Log($"invoking sort: [{string.Join(", ", numbers)}], correlationId={correlationId}");

        var (responses, _) = await invoker.InvokeStreamingCommandAsync(
            NumberStream(numbers),
            new RequestStreamMetadata { CorrelationId = correlationId });

        List<int> received = new();
        await foreach (StreamingExtendedResponse<string> response in responses.Entries)
        {
            received.Add(int.Parse(response.Payload));
        }

        Log($"exchange complete: invoker received [{string.Join(", ", received)}]");

        Assert.Equal(expected, received);
    }

    private (IAsyncEnumerable<StreamingExtendedResponse<string>> Responses, ResponseStreamMetadata Metadata) SortHandler(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests,
        RequestStreamMetadata requestMetadata,
        IExchangeContext exchange) =>
        (SortResponses(requests), new ResponseStreamMetadata());

    private async IAsyncEnumerable<StreamingExtendedResponse<string>> SortResponses(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests)
    {
        List<int> numbers = new();
        await foreach (ReceivedStreamingExtendedRequest<string> request in requests.Entries)
        {
            numbers.Add(int.Parse(request.Payload));
        }

        numbers.Sort();
        Log($"handler: buffered {numbers.Count} request(s), sorted [{string.Join(", ", numbers)}]");

        foreach (int value in numbers)
        {
            yield return new StreamingExtendedResponse<string>(value.ToString());
        }
    }

    private async IAsyncEnumerable<StreamingExtendedRequest<string>> NumberStream(int[] numbers)
    {
        foreach (int value in numbers)
        {
            await Task.Yield();
            yield return new StreamingExtendedRequest<string>(value.ToString());
        }
    }
}
