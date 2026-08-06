// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.RPC;
using TestEnvoys;
using Xunit.Abstractions;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

// End-to-end POC for RPC chunking against a real broker: an oversized request, an oversized
// response, both at once, a small payload that must not be chunked, and a real file carried as a
// single logical payload. Chunking is meant to be invisible, so every scenario goes through the
// ordinary CommandInvoker/CommandExecutor API and asserts the payload arrives intact.
public class ChunkingPocTests(ITestOutputHelper output)
{
    // The invoker splits above ChunkingConstants.PlaceholderMaxPacketSize (64 KB) minus overhead,
    // independently of what the broker would have accepted, so anything materially larger than
    // this is guaranteed to travel as multiple chunks.
    private const int ChunkThresholdBytes = 64 * 1024;

    private const string ChunkUserPropertyName = "__chunk";

    [CommandTopic("rpc/chunking/poc/echo")]
    private sealed class EchoInvoker(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : CommandInvoker<string, string>(appContext, mqttClient, "echo", new Utf8JsonSerializer());

    [CommandTopic("rpc/chunking/poc/echo")]
    private sealed class EchoExecutor(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : CommandExecutor<string, string>(appContext, mqttClient, "echo", new Utf8JsonSerializer());

    [CommandTopic("rpc/chunking/poc/filetransfer")]
    private sealed class FileTransferInvoker(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : CommandInvoker<string, string>(appContext, mqttClient, "filetransfer", new Utf8JsonSerializer());

    [CommandTopic("rpc/chunking/poc/filetransfer")]
    private sealed class FileTransferExecutor(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : CommandExecutor<string, string>(appContext, mqttClient, "filetransfer", new Utf8JsonSerializer());

    private void Log(string message) =>
        output.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  {message}");

    // Forwards the SDK's own System.Diagnostics.Trace output into the test log, so a run shows the
    // splitting, buffering and reassembly the envoys did rather than only the test's own view.
    private TraceCapture CaptureSdkTrace() => new(output);

    private sealed class TraceCapture : TraceListener
    {
        private readonly ITestOutputHelper _output;
        private volatile bool _detached;

        public TraceCapture(ITestOutputHelper output)
        {
            _output = output;
            Trace.Listeners.Add(this);
            Trace.AutoFlush = true;
        }

        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message) =>
            Emit(eventType, message);

        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args) =>
            Emit(eventType, args == null || format == null ? format : string.Format(CultureInfo.InvariantCulture, format, args));

        public override void Write(string? message)
        {
            // Trace.TraceXxx routes through TraceEvent; raw writes would only duplicate the header.
        }

        public override void WriteLine(string? message)
        {
        }

        private void Emit(TraceEventType eventType, string? message)
        {
            if (_detached || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            try
            {
                _output.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  [sdk {eventType,-11}] {message.TrimEnd()}");
            }
            catch (InvalidOperationException)
            {
                // The test finished and xUnit closed the output helper.
            }
        }

        protected override void Dispose(bool disposing)
        {
            _detached = true;
            Trace.Listeners.Remove(this);
            base.Dispose(disposing);
        }
    }

    // Frames a scenario line with separators so it stands out in the captured log.
    private void LogScenario(string message)
    {
        Log(new string('=', 80));
        Log(message);
        Log(new string('=', 80));
    }

    // Frames an integrity line the same way, so the sent and received hashes are easy to compare.
    private void LogIntegrity(string message)
    {
        Log(new string('-', 80));
        Log(message);
        Log(new string('-', 80));
    }

    // A request several times the chunk threshold, answered with a short acknowledgement.
    [Fact]
    public async Task LargeRequest_RoundTrips()
    {
        const int payloadSize = 1_000_000;
        Assert.True(payloadSize > ChunkThresholdBytes, "The payload must cross the chunk threshold for this scenario to mean anything.");

        using TraceCapture trace = CaptureSdkTrace();
        LogScenario($"SCENARIO large request: invoker sends a {payloadSize:N0} byte payload, which the invoker splits into chunks; the executor reassembles it and replies with a short ack.");

        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        string request = BuildPayload(payloadSize);
        string? executorSaw = null;

        await using EchoExecutor executor = new(appContext, executorClient)
        {
            OnCommandReceived = (received, ct) =>
            {
                executorSaw = received.Request;
                Log($"executor: reassembled {received.Request.Length:N0} chars");
                return Task.FromResult(new ExtendedResponse<string> { Response = "ack" });
            },
        };
        await executor.StartAsync();

        await using EchoInvoker invoker = new(appContext, invokerClient);

        Log($"invoking with a {request.Length:N0} char request");
        ExtendedResponse<string> response = await invoker.InvokeCommandAsync(request, new CommandRequestMetadata(), commandTimeout: TimeSpan.FromMinutes(2));

        LogIntegrity($"INTEGRITY request : chars={request.Length:N0} fnv1a64={ComputeFnv1a64(request)}");
        LogIntegrity($"INTEGRITY received: chars={executorSaw?.Length ?? 0:N0} fnv1a64={(executorSaw == null ? "n/a" : ComputeFnv1a64(executorSaw))}");

        Assert.Equal("ack", response.Response);
        Assert.Equal(request, executorSaw);
    }

    // A short request answered with a response several times the chunk threshold.
    [Fact]
    public async Task LargeResponse_RoundTrips()
    {
        const int payloadSize = 1_000_000;
        Assert.True(payloadSize > ChunkThresholdBytes, "The payload must cross the chunk threshold for this scenario to mean anything.");

        using TraceCapture trace = CaptureSdkTrace();
        LogScenario($"SCENARIO large response: invoker sends a short request; the executor replies with a {payloadSize:N0} byte payload, which it splits into chunks and the invoker reassembles.");

        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        string expected = BuildPayload(payloadSize);

        await using EchoExecutor executor = new(appContext, executorClient)
        {
            OnCommandReceived = (received, ct) =>
            {
                Log($"executor: replying with {expected.Length:N0} chars");
                return Task.FromResult(new ExtendedResponse<string> { Response = expected });
            },
        };
        await executor.StartAsync();

        await using EchoInvoker invoker = new(appContext, invokerClient);

        ExtendedResponse<string> response = await invoker.InvokeCommandAsync("give me a lot", new CommandRequestMetadata(), commandTimeout: TimeSpan.FromMinutes(2));

        LogIntegrity($"INTEGRITY sent    : chars={expected.Length:N0} fnv1a64={ComputeFnv1a64(expected)}");
        LogIntegrity($"INTEGRITY received: chars={response.Response.Length:N0} fnv1a64={ComputeFnv1a64(response.Response)}");

        Assert.Equal(expected, response.Response);
    }

    // Both directions oversized at once, so request and response reassembly run in the same invocation.
    [Fact]
    public async Task LargeRequestAndLargeResponse_RoundTrip()
    {
        using TraceCapture trace = CaptureSdkTrace();
        LogScenario("SCENARIO both directions: an oversized request is chunked to the executor, which echoes it back oversized so the response is chunked too.");

        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        string request = BuildPayload(300_000);

        await using EchoExecutor executor = new(appContext, executorClient)
        {
            OnCommandReceived = (received, ct) =>
            {
                Log($"executor: echoing {received.Request.Length:N0} chars straight back");
                return Task.FromResult(new ExtendedResponse<string> { Response = received.Request });
            },
        };
        await executor.StartAsync();

        await using EchoInvoker invoker = new(appContext, invokerClient);

        ExtendedResponse<string> response = await invoker.InvokeCommandAsync(request, new CommandRequestMetadata(), commandTimeout: TimeSpan.FromMinutes(2));

        LogIntegrity($"INTEGRITY request : chars={request.Length:N0} fnv1a64={ComputeFnv1a64(request)}");
        LogIntegrity($"INTEGRITY response: chars={response.Response.Length:N0} fnv1a64={ComputeFnv1a64(response.Response)}");

        Assert.Equal(request, response.Response);
    }

    // Guards the ordinary path: a payload below the threshold must go out as exactly one PUBLISH,
    // carrying no chunk metadata at all.
    [Fact]
    public async Task SmallRequest_IsNotChunked()
    {
        using TraceCapture trace = CaptureSdkTrace();
        LogScenario("SCENARIO small request: a payload below the chunk threshold must travel as a single message with no chunk metadata.");

        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient observerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        List<MqttApplicationMessage> observed = await ObserveRequestTopicAsync(observerClient, "rpc/chunking/poc/echo");

        await using EchoExecutor executor = new(appContext, executorClient)
        {
            OnCommandReceived = (received, ct) => Task.FromResult(new ExtendedResponse<string> { Response = received.Request }),
        };
        await executor.StartAsync();

        await using EchoInvoker invoker = new(appContext, invokerClient);

        const string request = "small enough to fit in one packet";
        ExtendedResponse<string> response = await invoker.InvokeCommandAsync(request, new CommandRequestMetadata(), commandTimeout: TimeSpan.FromSeconds(30));

        Assert.Equal(request, response.Response);

        Log($"observer saw {observed.Count} request message(s) on the wire");
        Assert.Single(observed);
        Assert.DoesNotContain(observed[0].UserProperties ?? [], p => p.Name == ChunkUserPropertyName);
    }

    // Proves the wire format: an oversized request really is several PUBLISHes, each tagged, all
    // sharing one correlation, with exactly one head chunk.
    [Fact]
    public async Task LargeRequest_TravelsAsSeveralTaggedChunks()
    {
        using TraceCapture trace = CaptureSdkTrace();
        LogScenario("SCENARIO wire format: a third client watches the request topic while an oversized request goes out, and checks the chunk metadata on what it sees.");

        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient observerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        List<MqttApplicationMessage> observed = await ObserveRequestTopicAsync(observerClient, "rpc/chunking/poc/echo");

        await using EchoExecutor executor = new(appContext, executorClient)
        {
            OnCommandReceived = (received, ct) => Task.FromResult(new ExtendedResponse<string> { Response = "ack" }),
        };
        await executor.StartAsync();

        await using EchoInvoker invoker = new(appContext, invokerClient);

        string request = BuildPayload(300_000);
        ExtendedResponse<string> response = await invoker.InvokeCommandAsync(request, new CommandRequestMetadata(), commandTimeout: TimeSpan.FromMinutes(2));
        Assert.Equal("ack", response.Response);

        List<string> chunkValues = observed
            .Select(m => m.UserProperties?.FirstOrDefault(p => p.Name == ChunkUserPropertyName)?.Value)
            .Where(v => v != null)
            .Select(v => v!)
            .ToList();

        foreach (string value in chunkValues)
        {
            Log($"observed {ChunkUserPropertyName}: {value}");
        }

        Assert.True(observed.Count > 1, $"Expected several chunks, observed {observed.Count} message(s).");
        Assert.Equal(observed.Count, chunkValues.Count);

        // Exactly one head chunk, the rest data chunks, and every chunk names the same message.
        Assert.Single(chunkValues, v => v.StartsWith("h:", StringComparison.Ordinal));
        Assert.Equal(chunkValues.Count - 1, chunkValues.Count(v => v.StartsWith("d:", StringComparison.Ordinal)));

        string messageId = chunkValues[0].Split(':')[1];
        Assert.All(chunkValues, v => Assert.Equal(messageId, v.Split(':')[1]));

        // Every chunk must carry a positive expiry, and none may outlive the invocation.
        Assert.All(observed, m => Assert.InRange(m.MessageExpiryInterval, 1u, (uint)TimeSpan.FromMinutes(2).TotalSeconds));

        // Only the head chunk carries the full property set.
        MqttApplicationMessage head = observed.Single(m =>
            m.UserProperties!.Single(p => p.Name == ChunkUserPropertyName).Value.StartsWith("h:", StringComparison.Ordinal));
        Assert.Contains(head.UserProperties!, p => p.Name == "__srcId");

        // __ts is consumed by the executor to advance the application clock, so it has to survive
        // reassembly: it rides on the head chunk, whose properties the reassembled message inherits.
        Assert.Contains(head.UserProperties!, p => p.Name == "__ts");

        foreach (MqttApplicationMessage tail in observed.Where(m => m != head))
        {
            Assert.DoesNotContain(tail.UserProperties!, p => p.Name == "__srcId");
            Assert.DoesNotContain(tail.UserProperties!, p => p.Name == "__ts");
            Assert.Contains(tail.UserProperties!, p => p.Name == "$partition");
            Assert.Contains(tail.UserProperties!, p => p.Name == "__protVer");
        }
    }

    // The scenario chunking actually exists for: one file, one logical payload, split transparently.
    private const string AdrRelativePath = "doc/dev/rpc-chunking-poc-plan.md";

    [Fact]
    public async Task FileTransfer()
    {
        using TraceCapture trace = CaptureSdkTrace();
        LogScenario($"SCENARIO file transfer: the executor reads '{AdrRelativePath}' and returns it as a single payload; chunking carries it and the invoker re-hashes what it got.");

        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        string? executorHash = null;

        await using FileTransferExecutor executor = new(appContext, executorClient)
        {
            OnCommandReceived = async (received, ct) =>
            {
                string contents = await File.ReadAllTextAsync(ResolveRepoFile(received.Request), ct);

                // Repeated so the payload exceeds the chunk threshold regardless of how the file
                // grows or shrinks; otherwise this scenario could quietly stop chunking at all.
                StringBuilder payload = new(ChunkThresholdBytes * 2);
                while (payload.Length <= ChunkThresholdBytes * 2)
                {
                    payload.Append(contents);
                }

                string body = payload.ToString();
                executorHash = ComputeFnv1a64(body);
                LogIntegrity($"INTEGRITY executor: file='{received.Request}' fileChars={contents.Length:N0} sentChars={body.Length:N0} fnv1a64={executorHash}");

                return new ExtendedResponse<string> { Response = body };
            },
        };
        await executor.StartAsync();

        await using FileTransferInvoker invoker = new(appContext, invokerClient);

        ExtendedResponse<string> response = await invoker.InvokeCommandAsync(AdrRelativePath, new CommandRequestMetadata(), commandTimeout: TimeSpan.FromMinutes(2));

        string invokerHash = ComputeFnv1a64(response.Response);
        LogIntegrity($"INTEGRITY invoker : chars={response.Response.Length:N0} fnv1a64={invokerHash}");

        Assert.NotNull(executorHash);
        Assert.Equal(executorHash, invokerHash);

        // Guards the point of the scenario: a payload that never crossed the threshold would have
        // round-tripped without exercising chunking at all.
        Assert.True(
            response.Response.Length > ChunkThresholdBytes,
            $"Payload was only {response.Response.Length:N0} chars, so nothing was chunked.");
    }

    // Subscribes a bystander client to the request topic and collects what the broker forwards, so a
    // test can inspect the wire without reaching into the SDK's internals.
    private static async Task<List<MqttApplicationMessage>> ObserveRequestTopicAsync(MqttSessionClient client, string topic)
    {
        List<MqttApplicationMessage> observed = new();

        client.ApplicationMessageReceivedAsync += args =>
        {
            lock (observed)
            {
                observed.Add(args.ApplicationMessage);
            }

            return Task.CompletedTask;
        };

        await client.SubscribeAsync(new MqttClientSubscribeOptions(topic, MqttQualityOfServiceLevel.AtLeastOnce));
        return observed;
    }

    private static string BuildPayload(int approximateBytes)
    {
        // Varied content rather than a single repeated character, so a reassembly bug that
        // duplicated or reordered a chunk cannot still produce a matching payload.
        StringBuilder builder = new(approximateBytes + 32);
        int line = 0;

        while (builder.Length < approximateBytes)
        {
            builder.Append($"line {line++:D8} 0123456789abcdefghijklmnopqrstuvwxyz\n");
        }

        return builder.ToString();
    }

    // FNV-1a 64-bit over the UTF-8 bytes. Not cryptographic; it is only here so both sides can run
    // identical, dependency-free arithmetic over the same content for a quick eyeball comparison.
    private static string ComputeFnv1a64(string value)
    {
        const ulong OffsetBasis = 14695981039346656037;
        const ulong Prime = 1099511628211;

        ulong hash = OffsetBasis;
        foreach (byte b in Encoding.UTF8.GetBytes(value))
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
