// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Streaming;
using TestEnvoys;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

// Flow control (ADR 25): a producer takes its next entry only after the broker accepted the previous one; a consumer
// holds received entries in a bounded buffer and ends the exchange with an Overflow status (s:<index>, __stat 429) when
// an entry arrives while it is full. A consumer that needs to set the pace asks for batches on the request stream.
public partial class StreamingPocTests
{
    [CommandTopic("rpc/streaming/poc/flow/pacing")]
    private sealed class PacingInvoker(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandInvoker<string, string>(appContext, mqttClient, "pacing", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/flow/pacing")]
    private sealed class PacingExecutor(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandExecutor<string, string>(appContext, mqttClient, "pacing", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/flow/invokeroverflow")]
    private sealed class InvokerOverflowInvoker(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandInvoker<string, string>(appContext, mqttClient, "invokeroverflow", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/flow/invokeroverflow")]
    private sealed class InvokerOverflowExecutor(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandExecutor<string, string>(appContext, mqttClient, "invokeroverflow", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/flow/executoroverflow")]
    private sealed class ExecutorOverflowInvoker(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandInvoker<string, string>(appContext, mqttClient, "executoroverflow", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/flow/executoroverflow")]
    private sealed class ExecutorOverflowExecutor(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandExecutor<string, string>(appContext, mqttClient, "executoroverflow", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/flow/pull")]
    private sealed class PullInvoker(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandInvoker<string, string>(appContext, mqttClient, "pull", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/flow/pull")]
    private sealed class PullExecutor(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandExecutor<string, string>(appContext, mqttClient, "pull", new Utf8JsonSerializer());

    // The executor's handler produces entries as fast as it is asked. The SDK asks for entry i only after the broker
    // accepted entry i-1, so the producer never runs ahead of the broker.
    [Fact]
    public async Task FlowControl_ProducerPacedByBroker()
    {
        const int count = 20;
        LogScenario($"SCENARIO flow control, producer pacing: the executor's handler yields {count} entries; each is taken only after the broker accepted the previous one.");
        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        List<string> events = new();
        void Record(string line)
        {
            Log(line);
            lock (events)
            {
                events.Add(line);
            }
        }

        await using PacingExecutor executor = new(appContext, executorClient)
        {
            OnStreamingCommandReceived = (requests, _, _) => (RecordedProducer(requests, count, Record), new ResponseStreamMetadata()),
            Log = Record,
        };
        await executor.StartAsync();

        await using PacingInvoker invoker = new(appContext, invokerClient) { Log = Log };

        List<string> received = new();
        await foreach (string item in invoker.ExecuteStreamingAsync("go"))
        {
            received.Add(item);
        }

        Assert.Equal(Enumerable.Range(0, count).Select(i => $"item {i}"), received);

        List<string> snapshot;
        lock (events)
        {
            snapshot = new(events);
        }

        for (int i = 1; i < count; i++)
        {
            int accepted = snapshot.IndexOf($"executor: response idx={i - 1} accepted by the broker");
            int produced = snapshot.IndexOf($"producer: yields item {i}");
            Assert.True(accepted >= 0 && produced > accepted, $"item {i} was taken before the broker accepted item {i - 1}");
        }
    }

    private static async IAsyncEnumerable<StreamingExtendedResponse<string>> RecordedProducer(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests, int count, Action<string> record)
    {
        await foreach (ReceivedStreamingExtendedRequest<string> _ in requests.Entries)
        {
        }

        for (int i = 0; i < count; i++)
        {
            record($"producer: yields item {i}");
            yield return new StreamingExtendedResponse<string>($"item {i}");
        }
    }

    // The caller doesn't read its responses while the executor streams. The invoker's buffer fills at its limit; the
    // next entry ends the exchange with Overflow. The caller then receives the buffered entries followed by the overflow
    // error, and the executor's exchange ends with the overflow reported by the peer, so it stops producing.
    [Fact]
    public async Task FlowControl_InvokerBufferOverflow()
    {
        const int bufferLimit = 5;
        const int wouldProduce = 50;
        LogScenario($"SCENARIO flow control, invoker overflow: the caller doesn't read; its buffer holds {bufferLimit} responses; entry {bufferLimit} ends the exchange with Overflow (429).");
        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        TaskCompletionSource<IExchangeContext> executorExchange = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int produced = 0;

        await using InvokerOverflowExecutor executor = new(appContext, executorClient)
        {
            OnStreamingCommandReceived = (requests, _, exchange) =>
            {
                executorExchange.TrySetResult(exchange);
                return (SteadyProducer(requests, wouldProduce, () => Interlocked.Increment(ref produced)), new ResponseStreamMetadata());
            },
            Log = Log,
        };
        await executor.StartAsync();

        await using InvokerOverflowInvoker invoker = new(appContext, invokerClient) { Log = Log, StreamBufferLimit = bufferLimit };

        var (responses, exchange) = await invoker.InvokeStreamingCommandAsync(OneRequest("tags"), exchangeTimeout: TimeSpan.FromMinutes(1));

        // Don't read until the exchange has ended.
        StreamOverflowException localOverflow = await Assert.ThrowsAsync<StreamOverflowException>(() => exchange.Completion.WaitAsync(TimeSpan.FromSeconds(10)));

        List<string> received = new();
        StreamOverflowException streamError = await Assert.ThrowsAsync<StreamOverflowException>(async () =>
        {
            await foreach (StreamingExtendedResponse<string> response in responses.Entries)
            {
                received.Add(response.Payload);
            }
        });

        Assert.Same(localOverflow, streamError);
        Assert.False(localOverflow.IsRemote);
        Assert.Equal((uint)bufferLimit, localOverflow.Index);
        Assert.Equal(Enumerable.Range(0, bufferLimit).Select(i => $"item {i}"), received);

        IExchangeContext executorSide = await executorExchange.Task;
        StreamOverflowException remoteOverflow = await Assert.ThrowsAsync<StreamOverflowException>(() => executorSide.Completion.WaitAsync(TimeSpan.FromSeconds(10)));
        Log($"invoker: received {received.Count} item(s), then overflow at idx={localOverflow.Index}; executor saw remote overflow at idx={remoteOverflow.Index} after producing {Volatile.Read(ref produced)} of {wouldProduce}");

        Assert.True(remoteOverflow.IsRemote);
        Assert.Equal((uint)bufferLimit, remoteOverflow.Index);
        Assert.True(Volatile.Read(ref produced) < wouldProduce, "the executor kept producing after the overflow");
    }

    private static async IAsyncEnumerable<StreamingExtendedResponse<string>> SteadyProducer(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests, int count, Action onProduced)
    {
        await foreach (ReceivedStreamingExtendedRequest<string> _ in requests.Entries)
        {
        }

        for (int i = 0; i < count; i++)
        {
            onProduced();
            yield return new StreamingExtendedResponse<string>($"item {i}");
            await Task.Delay(20);
        }
    }

    // The mirror case: the executor's handler doesn't read its requests while the invoker uploads. The executor's
    // buffer fills; the next request ends the exchange with Overflow on the response topic. The handler then receives
    // the buffered requests followed by the overflow error, and the invoker's response stream ends with the overflow
    // reported by the peer, so it stops sending requests.
    [Fact]
    public async Task FlowControl_ExecutorBufferOverflow()
    {
        const int bufferLimit = 3;
        const int wouldSend = 20;
        LogScenario($"SCENARIO flow control, executor overflow: the handler doesn't read; its buffer holds {bufferLimit} requests; request {bufferLimit} ends the exchange with Overflow (429).");
        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        TaskCompletionSource<Exception> handlerSaw = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> handlerReceived = new();

        await using ExecutorOverflowExecutor executor = new(appContext, executorClient)
        {
            OnStreamingCommandReceived = (requests, _, exchange) => (ReadAfterExchangeEnds(requests, exchange, handlerReceived, handlerSaw), new ResponseStreamMetadata()),
            Log = Log,
            StreamBufferLimit = bufferLimit,
        };
        await executor.StartAsync();

        await using ExecutorOverflowInvoker invoker = new(appContext, invokerClient) { Log = Log };

        int pulled = 0;
        var (responses, exchange) = await invoker.InvokeStreamingCommandAsync(
            SteadyRequests(wouldSend, () => Interlocked.Increment(ref pulled)),
            exchangeTimeout: TimeSpan.FromMinutes(1));

        StreamOverflowException remoteOverflow = await Assert.ThrowsAsync<StreamOverflowException>(async () =>
        {
            await foreach (StreamingExtendedResponse<string> _ in responses.Entries)
            {
            }
        });
        Assert.Same(remoteOverflow, await Assert.ThrowsAsync<StreamOverflowException>(() => exchange.Completion));

        Exception handlerError = await handlerSaw.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Log($"executor: handler received [{string.Join(", ", handlerReceived)}], then {handlerError.GetType().Name}; invoker saw remote overflow at idx={remoteOverflow.Index} after taking {Volatile.Read(ref pulled)} of {wouldSend} requests");

        Assert.True(remoteOverflow.IsRemote);
        Assert.Equal((uint)bufferLimit, remoteOverflow.Index);
        Assert.True(Volatile.Read(ref pulled) < wouldSend, "the invoker kept sending after the overflow");

        StreamOverflowException localOverflow = Assert.IsType<StreamOverflowException>(handlerError);
        Assert.False(localOverflow.IsRemote);
        Assert.Equal((uint)bufferLimit, localOverflow.Index);
        Assert.Equal(Enumerable.Range(0, bufferLimit).Select(i => $"chunk {i}"), handlerReceived);
    }

    private static async IAsyncEnumerable<StreamingExtendedResponse<string>> ReadAfterExchangeEnds(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests,
        IExchangeContext exchange,
        List<string> received,
        TaskCompletionSource<Exception> saw)
    {
        try
        {
            await exchange.Completion;
        }
        catch (Exception)
        {
            // Expected: the overflow ended the exchange. The buffered requests are still readable.
        }

        try
        {
            await foreach (ReceivedStreamingExtendedRequest<string> request in requests.Entries)
            {
                received.Add(request.Payload);
            }

            saw.TrySetException(new InvalidOperationException("The request stream ended without an error."));
        }
        catch (Exception ex)
        {
            saw.TrySetResult(ex);
        }

        yield break;
    }

    private static async IAsyncEnumerable<StreamingExtendedRequest<string>> SteadyRequests(int count, Action onPulled)
    {
        for (int i = 0; i < count; i++)
        {
            onPulled();
            yield return new StreamingExtendedRequest<string>($"chunk {i}");
            await Task.Delay(20);
        }
    }

    // The consumer sets the pace with the existing protocol: the invoker asks for a batch on the request stream and the
    // executor answers with that many entries before waiting for the next request. A slow caller with a buffer of one
    // batch receives every item and never overflows.
    [Fact]
    public async Task FlowControl_ConsumerPacedPull()
    {
        const int batchSize = 5;
        const int batches = 6;
        LogScenario($"SCENARIO flow control, consumer-paced pull: the invoker asks for {batches} batches of {batchSize}; its buffer holds one batch; a slow reader never overflows.");
        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        await using PullExecutor executor = new(appContext, executorClient)
        {
            OnStreamingCommandReceived = (requests, _, _) => (AnswerBatches(requests), new ResponseStreamMetadata()),
            Log = Log,
        };
        await executor.StartAsync();

        await using PullInvoker invoker = new(appContext, invokerClient) { Log = Log, StreamBufferLimit = batchSize };

        using SemaphoreSlim batchConsumed = new(0);
        var (responses, exchange) = await invoker.InvokeStreamingCommandAsync(
            AskForBatches(batches, batchSize, batchConsumed),
            exchangeTimeout: TimeSpan.FromMinutes(1));

        List<string> received = new();
        await foreach (StreamingExtendedResponse<string> response in responses.Entries)
        {
            received.Add(response.Payload);
            await Task.Delay(10);
            if (received.Count % batchSize == 0)
            {
                batchConsumed.Release();
            }
        }

        await exchange.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(Enumerable.Range(0, batches * batchSize).Select(i => $"item {i}"), received);
    }

    private static async IAsyncEnumerable<StreamingExtendedRequest<string>> AskForBatches(int batches, int batchSize, SemaphoreSlim batchConsumed)
    {
        for (int b = 0; b < batches; b++)
        {
            yield return new StreamingExtendedRequest<string>(batchSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await batchConsumed.WaitAsync();
        }
    }

    private static async IAsyncEnumerable<StreamingExtendedResponse<string>> AnswerBatches(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests)
    {
        int next = 0;
        await foreach (ReceivedStreamingExtendedRequest<string> request in requests.Entries)
        {
            int size = int.Parse(request.Payload, System.Globalization.CultureInfo.InvariantCulture);
            for (int i = 0; i < size; i++)
            {
                yield return new StreamingExtendedResponse<string>($"item {next++}");
            }
        }
    }
}
