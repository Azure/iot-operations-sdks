// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Streaming;
using TestEnvoys;
using Xunit.Abstractions;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

// Minimal end-to-end POC for streaming RPC: ping-pong (1:1, immediate), sort-array (buffer-all),
// and file transfer (short request stream, file streamed back line by line with a hash logged on both sides).
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

    [CommandTopic("rpc/streaming/poc/filetransfer")]
    private sealed class FileTransferInvoker(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandInvoker<string, string>(appContext, mqttClient, "filetransfer", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/filetransfer")]
    private sealed class FileTransferExecutor(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandExecutor<string, string>(appContext, mqttClient, "filetransfer", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/browse")]
    private sealed class BrowseInvoker(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandInvoker<string, string>(appContext, mqttClient, "browse", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/browse")]
    private sealed class BrowseExecutor(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandExecutor<string, string>(appContext, mqttClient, "browse", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/historicread")]
    private sealed class HistoricReadInvoker(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandInvoker<string, string>(appContext, mqttClient, "historicread", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/historicread")]
    private sealed class HistoricReadExecutor(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandExecutor<string, string>(appContext, mqttClient, "historicread", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/wottd")]
    private sealed class WotTdInvoker(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandInvoker<string, string>(appContext, mqttClient, "wottd", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/wottd")]
    private sealed class WotTdExecutor(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandExecutor<string, string>(appContext, mqttClient, "wottd", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/openusd")]
    private sealed class OpenUsdInvoker(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandInvoker<string, string>(appContext, mqttClient, "openusd", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/openusd")]
    private sealed class OpenUsdExecutor(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandExecutor<string, string>(appContext, mqttClient, "openusd", new Utf8JsonSerializer());

    private void Log(string message) =>
        output.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  {message}");

    // Frames a scenario line with separators so it stands out in the captured log.
    private void LogScenario(string message)
    {
        Log(new string('=', 80));
        Log(message);
        Log(new string('=', 80));
    }

    // Frames an integrity line the same way, so the executor and invoker hashes are easy to spot and compare.
    private void LogIntegrity(string message)
    {
        Log(new string('-', 80));
        Log(message);
        Log(new string('-', 80));
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

    // File transfer: invoker sends a short request stream naming a repo file; executor hashes the file,
    // logs the hash, then streams it back one line per response entry. The invoker re-hashes the lines it
    // received once the response stream closes and logs that hash, so the two can be compared by eye.
    private const string AdrRelativePath = "doc/dev/adr/0025-rpc-streaming.md";

    // Set by the executor-side handler so the test can assert what was only logged for manual comparison.
    private string? _executorFileHash;

    [Fact]
    public async Task FileTransfer()
    {
        LogScenario($"SCENARIO file transfer: invoker requests '{AdrRelativePath}'; executor hashes the file and logs the hash, then streams it back one line per entry; invoker re-hashes on stream close and logs it for manual comparison.");
        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        await using FileTransferExecutor executor = new(appContext, executorClient)
        {
            OnStreamingCommandReceived = FileTransferHandler,
            Log = Log,
        };
        await executor.StartAsync();

        await using FileTransferInvoker invoker = new(appContext, invokerClient)
        {
            Log = Log,
        };

        Guid correlationId = Guid.NewGuid();
        Log($"invoking file transfer: '{AdrRelativePath}', correlationId={correlationId}");

        // The file is streamed a line at a time, so the exchange budget has to cover hundreds of entries.
        var (responses, _) = await invoker.InvokeStreamingCommandAsync(
            FileRequestStream(AdrRelativePath),
            new RequestStreamMetadata { CorrelationId = correlationId },
            exchangeTimeout: TimeSpan.FromMinutes(5));

        List<string> received = new();
        await foreach (StreamingExtendedResponse<string> response in responses.Entries)
        {
            received.Add(response.Payload);
        }

        // The response stream has closed (`last` seen), so every line is in hand: hash it the same way the executor did.
        string invokerHash = ComputeFnv1a64(received);
        LogIntegrity($"INTEGRITY invoker : lines={received.Count} fnv1a64={invokerHash}");

        Log($"exchange complete: invoker received {received.Count} line(s)");

        Assert.NotEmpty(received);
        Assert.NotNull(_executorFileHash);
        Assert.Equal(_executorFileHash, invokerHash);
    }

    // A short request stream: the file to transfer, then the chunking mode the invoker wants.
    private async IAsyncEnumerable<StreamingExtendedRequest<string>> FileRequestStream(string relativePath)
    {
        await Task.Yield();
        yield return new StreamingExtendedRequest<string>(relativePath);
        await Task.Yield();
        yield return new StreamingExtendedRequest<string>("lines");
    }

    private (IAsyncEnumerable<StreamingExtendedResponse<string>> Responses, ResponseStreamMetadata Metadata) FileTransferHandler(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests,
        RequestStreamMetadata requestMetadata,
        IExchangeContext exchange) =>
        (FileLineResponses(requests), new ResponseStreamMetadata());

    private async IAsyncEnumerable<StreamingExtendedResponse<string>> FileLineResponses(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests)
    {
        // Buffer the (short) request stream first: entry 0 names the file, entry 1 selects the chunking mode.
        List<string> requestEntries = new();
        await foreach (ReceivedStreamingExtendedRequest<string> request in requests.Entries)
        {
            requestEntries.Add(request.Payload);
        }

        Log($"handler: request stream closed after {requestEntries.Count} entry(ies): [{string.Join(", ", requestEntries)}]");

        string relativePath = requestEntries[0];
        string mode = requestEntries.Count > 1 ? requestEntries[1] : "lines";
        if (mode != "lines")
        {
            throw new InvalidOperationException($"Unsupported chunking mode '{mode}'; this POC only streams files line by line.");
        }

        string absolutePath = ResolveRepoFile(relativePath);
        string[] lines = await File.ReadAllLinesAsync(absolutePath);

        // Hash before sending so the invoker's hash of what it received can be compared against it.
        string hash = ComputeFnv1a64(lines);
        _executorFileHash = hash;
        LogIntegrity($"INTEGRITY executor: file='{relativePath}' lines={lines.Length} fnv1a64={hash}");

        foreach (string line in lines)
        {
            yield return new StreamingExtendedResponse<string>(line);
        }
    }

    // Incremental OPC UA browse: a single request names the address space; the executor doesn't wait to
    // enumerate the whole space, it streams each discovered node back as soon as it's found, so the invoker
    // gets fast feedback and never has to hold the entire (potentially huge) address space in memory at once.
    [Theory]
    [InlineData(20)]
    public async Task IncrementalOpcUaBrowse(int nodeCount)
    {
        LogScenario($"SCENARIO incremental OPC UA browse: invoker requests the address space; executor streams {nodeCount} discovered node(s) back one at a time as it finds them, instead of buffering the whole space first.");
        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        await using BrowseExecutor executor = new(appContext, executorClient)
        {
            OnStreamingCommandReceived = BrowseHandler,
            Log = Log,
        };
        await executor.StartAsync();

        await using BrowseInvoker invoker = new(appContext, invokerClient)
        {
            Log = Log,
        };

        Guid correlationId = Guid.NewGuid();
        Log($"invoking browse: address space with {nodeCount} node(s), correlationId={correlationId}");

        // Doc's desired SDK experience: a single plain request in, a directly awaitable-foreach stream of plain items out.
        DateTime requestSentAt = DateTime.UtcNow;
        List<string> received = new();
        TimeSpan? firstNodeLatency = null;
        await foreach (string node in invoker.ExecuteStreamingAsync(
            nodeCount.ToString(),
            new RequestStreamMetadata { CorrelationId = correlationId }))
        {
            firstNodeLatency ??= DateTime.UtcNow - requestSentAt;
            received.Add(node);
        }

        Log($"exchange complete: invoker received {received.Count} node(s); first node arrived after {firstNodeLatency?.TotalMilliseconds:F0} ms, well before the full browse finished");

        Assert.Equal(nodeCount, received.Count);
        for (int i = 0; i < nodeCount; i++)
        {
            // Ordering guarantee: nodes must arrive in the same order the executor discovered/emitted them.
            Assert.Equal($"ns=2;s=Node{i}", received[i]);
        }
    }

    private (IAsyncEnumerable<StreamingExtendedResponse<string>> Responses, ResponseStreamMetadata Metadata) BrowseHandler(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests,
        RequestStreamMetadata requestMetadata,
        IExchangeContext exchange) =>
        (BrowseResponses(requests), new ResponseStreamMetadata());

    private async IAsyncEnumerable<StreamingExtendedResponse<string>> BrowseResponses(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests)
    {
        int nodeCount = 0;
        await foreach (ReceivedStreamingExtendedRequest<string> request in requests.Entries)
        {
            nodeCount = int.Parse(request.Payload);
        }

        for (int i = 0; i < nodeCount; i++)
        {
            // Simulates real discovery latency: each node is "found" on the server as time passes,
            // rather than all being known up front, which is exactly why buffer-then-return doesn't fit.
            await Task.Delay(10);
            string node = $"ns=2;s=Node{i}";
            Log($"handler: discovered {node}, streaming it immediately");
            yield return new StreamingExtendedResponse<string>(node);
        }
    }

    // Historic read / backfilling: a single request names the desired range; the executor streams every
    // record it produces back in strict emission order, so a consumer recovering from an outage gets exactly
    // the records the historian produced, in the same order, without waiting for the whole range to be read.
    [Theory]
    [InlineData(500)]
    public async Task HistoricReadBackfill(int recordCount)
    {
        LogScenario($"SCENARIO historic read / backfilling: invoker requests a historical range after an outage; executor streams {recordCount} record(s) back in the same order they were produced, with no gaps or reordering.");
        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        await using HistoricReadExecutor executor = new(appContext, executorClient)
        {
            OnStreamingCommandReceived = HistoricReadHandler,
            Log = Log,
        };
        await executor.StartAsync();

        await using HistoricReadInvoker invoker = new(appContext, invokerClient)
        {
            Log = Log,
        };

        Guid correlationId = Guid.NewGuid();
        Log($"invoking historic read: {recordCount} record(s), correlationId={correlationId}");

        List<string> received = new();
        await foreach (string record in invoker.ExecuteStreamingAsync(
            recordCount.ToString(),
            new RequestStreamMetadata { CorrelationId = correlationId },
            exchangeTimeout: TimeSpan.FromMinutes(2)))
        {
            received.Add(record);
        }

        Log($"exchange complete: invoker received {received.Count} record(s), backfill recovered without gaps");

        Assert.Equal(recordCount, received.Count);
        for (int i = 0; i < recordCount; i++)
        {
            // No record dropped, duplicated, or reordered during the backfill.
            Assert.Equal($"record {i} @2024-01-01T00:{i / 60:D2}:{i % 60:D2}Z", received[i]);
        }
    }

    private (IAsyncEnumerable<StreamingExtendedResponse<string>> Responses, ResponseStreamMetadata Metadata) HistoricReadHandler(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests,
        RequestStreamMetadata requestMetadata,
        IExchangeContext exchange) =>
        (HistoricReadResponses(requests), new ResponseStreamMetadata());

    private async IAsyncEnumerable<StreamingExtendedResponse<string>> HistoricReadResponses(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests)
    {
        int recordCount = 0;
        await foreach (ReceivedStreamingExtendedRequest<string> request in requests.Entries)
        {
            recordCount = int.Parse(request.Payload);
        }

        Log($"handler: replaying {recordCount} historian record(s) in original order");
        for (int i = 0; i < recordCount; i++)
        {
            yield return new StreamingExtendedResponse<string>($"record {i} @2024-01-01T00:{i / 60:D2}:{i % 60:D2}Z");
        }
    }

    // W3C WoT TD streaming: a single request asks for the discovered Thing Descriptions; the executor streams
    // each TD document back as it's discovered on the network, rather than waiting for the whole catalog of
    // devices to be enumerated and materialized into one large response first.
    [Theory]
    [InlineData(15)]
    public async Task WotThingDescriptionStreaming(int thingCount)
    {
        LogScenario($"SCENARIO W3C WoT TD streaming: invoker asks for discovered Thing Descriptions; executor streams {thingCount} TD document(s) back as each device is discovered, instead of materializing the whole catalog first.");
        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        await using WotTdExecutor executor = new(appContext, executorClient)
        {
            OnStreamingCommandReceived = WotTdHandler,
            Log = Log,
        };
        await executor.StartAsync();

        await using WotTdInvoker invoker = new(appContext, invokerClient)
        {
            Log = Log,
        };

        Guid correlationId = Guid.NewGuid();
        Log($"invoking WoT TD discovery: {thingCount} thing(s), correlationId={correlationId}");

        List<string> received = new();
        await foreach (string td in invoker.ExecuteStreamingAsync(
            thingCount.ToString(),
            new RequestStreamMetadata { CorrelationId = correlationId }))
        {
            received.Add(td);
        }

        Log($"exchange complete: invoker received {received.Count} Thing Description(s)");

        Assert.Equal(thingCount, received.Count);
        for (int i = 0; i < thingCount; i++)
        {
            string expectedTd = $"{{\"id\":\"urn:thing:{i}\",\"title\":\"Thing {i}\"}}";
            // Payload fidelity: each TD document arrives whole and unmodified, one per entry.
            Assert.Equal(expectedTd, received[i]);
        }
    }

    private (IAsyncEnumerable<StreamingExtendedResponse<string>> Responses, ResponseStreamMetadata Metadata) WotTdHandler(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests,
        RequestStreamMetadata requestMetadata,
        IExchangeContext exchange) =>
        (WotTdResponses(requests), new ResponseStreamMetadata());

    private async IAsyncEnumerable<StreamingExtendedResponse<string>> WotTdResponses(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests)
    {
        int thingCount = 0;
        await foreach (ReceivedStreamingExtendedRequest<string> request in requests.Entries)
        {
            thingCount = int.Parse(request.Payload);
        }

        for (int i = 0; i < thingCount; i++)
        {
            // Simulates each device announcing itself on the network at its own pace.
            await Task.Delay(10);
            string td = $"{{\"id\":\"urn:thing:{i}\",\"title\":\"Thing {i}\"}}";
            Log($"handler: discovered Thing Description {td}");
            yield return new StreamingExtendedResponse<string>(td);
        }
    }

    // OpenUSD artefact download: invoker requests a large engineering artefact; the executor chunks it and
    // streams the chunks back in order, and the invoker reconstructs the exact original bytes purely from
    // chunk order and content, with no bespoke chunking protocol layered on top of the SDK's streaming.
    private const int OpenUsdArtefactSizeBytes = 256 * 1024;
    private const int OpenUsdChunkSizeBytes = 8 * 1024;
    private const int OpenUsdRandomSeed = 20240516;
    private const int OpenUsdLogCharacterLimit = 256;

    [Fact]
    public async Task OpenUsdArtefactDownload()
    {
        LogScenario($"SCENARIO OpenUSD artefact download: invoker requests a {OpenUsdArtefactSizeBytes}-byte artefact; executor streams it back in {OpenUsdChunkSizeBytes}-byte chunks; invoker reconstructs the exact original bytes from chunk order alone.");
        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        await using OpenUsdExecutor executor = new(appContext, executorClient)
        {
            OnStreamingCommandReceived = OpenUsdHandler,
            Log = LogOpenUsd,
        };
        await executor.StartAsync();

        await using OpenUsdInvoker invoker = new(appContext, invokerClient)
        {
            Log = LogOpenUsd,
        };

        byte[] expectedArtefact = GenerateOpenUsdArtefact();

        Guid correlationId = Guid.NewGuid();
        Log($"invoking OpenUSD artefact download: {expectedArtefact.Length} byte(s), correlationId={correlationId}");

        using MemoryStream reconstructed = new();
        await foreach (string chunkBase64 in invoker.ExecuteStreamingAsync(
            "artefact.usdz",
            new RequestStreamMetadata { CorrelationId = correlationId },
            exchangeTimeout: TimeSpan.FromMinutes(2)))
        {
            byte[] chunk = Convert.FromBase64String(chunkBase64);
            reconstructed.Write(chunk, 0, chunk.Length);
        }

        byte[] reconstructedArtefact = reconstructed.ToArray();
        Log($"exchange complete: invoker reconstructed {reconstructedArtefact.Length} byte(s) from streamed chunks");

        // Payload fidelity: chunk boundaries plus chunk ordering are enough to deterministically
        // reconstruct the artefact, with no application-level chunk-numbering scheme required.
        Assert.Equal(expectedArtefact, reconstructedArtefact);
    }

    private void LogOpenUsd(string message) =>
        Log(message.Length <= OpenUsdLogCharacterLimit
            ? message
            : $"{message[..(OpenUsdLogCharacterLimit - 3)]}...");

    private (IAsyncEnumerable<StreamingExtendedResponse<string>> Responses, ResponseStreamMetadata Metadata) OpenUsdHandler(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests,
        RequestStreamMetadata requestMetadata,
        IExchangeContext exchange) =>
        (OpenUsdChunkResponses(requests), new ResponseStreamMetadata());

    private async IAsyncEnumerable<StreamingExtendedResponse<string>> OpenUsdChunkResponses(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests)
    {
        string artefactName = string.Empty;
        await foreach (ReceivedStreamingExtendedRequest<string> request in requests.Entries)
        {
            artefactName = request.Payload;
        }

        byte[] artefact = GenerateOpenUsdArtefact();
        Log($"handler: streaming artefact '{artefactName}' ({artefact.Length} bytes) in {OpenUsdChunkSizeBytes}-byte chunks");

        for (int offset = 0; offset < artefact.Length; offset += OpenUsdChunkSizeBytes)
        {
            int length = Math.Min(OpenUsdChunkSizeBytes, artefact.Length - offset);
            string chunk = Convert.ToBase64String(artefact, offset, length);
            yield return new StreamingExtendedResponse<string>(chunk);
        }
    }

    // Deterministic pseudo-random "artefact" bytes, generated identically on both sides from a fixed seed
    // so the test can assert exact byte-for-byte reconstruction without shipping a real binary fixture.
    private static byte[] GenerateOpenUsdArtefact()
    {
        byte[] artefact = new byte[OpenUsdArtefactSizeBytes];
        new Random(OpenUsdRandomSeed).NextBytes(artefact);
        return artefact;
    }

    // FNV-1a 64-bit over the UTF-8 bytes of the lines joined by "\n". Not cryptographic; it is only here so both
    // sides can run identical, dependency-free arithmetic over the same logical content for a manual eyeball check.
    private static string ComputeFnv1a64(IEnumerable<string> lines)
    {
        const ulong OffsetBasis = 14695981039346656037;
        const ulong Prime = 1099511628211;

        ulong hash = OffsetBasis;
        foreach (byte b in Encoding.UTF8.GetBytes(string.Join("\n", lines)))
        {
            hash ^= b;
            hash *= Prime;
        }

        return hash.ToString("x16");
    }

    // Tests run out of bin/, so walk up from the test binary until the repo-relative path resolves.
    private static string ResolveRepoFile(string relativePath)
    {
        string[] segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string candidate = Path.Combine(new[] { dir.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not locate '{relativePath}' in any ancestor of '{AppContext.BaseDirectory}'.");
    }
}
