// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Streaming;
using TestEnvoys;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

// Stream failure (ADR 25): a producer ends its own stream with a Failed status (s:0, __stat 500). An application error
// carries ADR 21's AppErrCode / AppErrPayload; an execution failure (a throwing handler or request stream) carries
// neither. The consumer first receives every entry produced before the failure, then its stream ends with the error.
public partial class StreamingPocTests
{
    [CommandTopic("rpc/streaming/poc/failure/apperror")]
    private sealed class AppErrorInvoker(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandInvoker<string, string>(appContext, mqttClient, "apperror", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/failure/apperror")]
    private sealed class AppErrorExecutor(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandExecutor<string, string>(appContext, mqttClient, "apperror", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/failure/executionerror")]
    private sealed class ExecutionErrorInvoker(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandInvoker<string, string>(appContext, mqttClient, "executionerror", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/failure/executionerror")]
    private sealed class ExecutionErrorExecutor(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandExecutor<string, string>(appContext, mqttClient, "executionerror", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/failure/requestfailure")]
    private sealed class RequestFailureInvoker(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandInvoker<string, string>(appContext, mqttClient, "requestfailure", new Utf8JsonSerializer());

    [CommandTopic("rpc/streaming/poc/failure/requestfailure")]
    private sealed class RequestFailureExecutor(ApplicationContext appContext, IMqttPubSubClient mqttClient)
        : StreamingCommandExecutor<string, string>(appContext, mqttClient, "requestfailure", new Utf8JsonSerializer());

    // Historic read whose source goes offline mid-backfill: the executor deliberately fails the stream with an
    // application error. The caller uses the single-request shortcut, receives the records read so far, and then its
    // await foreach throws the application error with the code and payload the executor reported.
    [Fact]
    public async Task StreamFailure_ExecutorApplicationError()
    {
        const int recordsBeforeFailure = 3;
        LogScenario($"SCENARIO stream failure, application error: executor streams {recordsBeforeFailure} records, then the historian goes offline and it fails the stream with AppErrCode 'sourceOffline'.");
        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        await using AppErrorExecutor executor = new(appContext, executorClient)
        {
            OnStreamingCommandReceived = (requests, _, _) => (BackfillThenSourceOffline(requests, recordsBeforeFailure), new ResponseStreamMetadata()),
            Log = Log,
        };
        await executor.StartAsync();

        await using AppErrorInvoker invoker = new(appContext, invokerClient) { Log = Log };

        List<string> received = new();
        StreamingApplicationException error = await Assert.ThrowsAsync<StreamingApplicationException>(async () =>
        {
            await foreach (string record in invoker.ExecuteStreamingAsync("boiler-7"))
            {
                received.Add(record);
            }
        });

        Log($"invoker: received {received.Count} record(s), then application error '{error.ErrorCode}' payload={error.ErrorPayload}");

        Assert.Equal(Enumerable.Range(0, recordsBeforeFailure).Select(i => $"boiler-7 record {i}"), received);
        Assert.True(error.IsRemote);
        Assert.Equal("sourceOffline", error.ErrorCode);
        Assert.Equal("{\"tag\":\"boiler-7\",\"lastIndex\":2}", error.ErrorPayload);
        Assert.Equal("Historian connection lost.", error.Message);
    }

    private static async IAsyncEnumerable<StreamingExtendedResponse<string>> BackfillThenSourceOffline(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests, int recordsBeforeFailure)
    {
        string tag = string.Empty;
        await foreach (ReceivedStreamingExtendedRequest<string> request in requests.Entries)
        {
            tag = request.Payload;
        }

        for (int i = 0; i < recordsBeforeFailure; i++)
        {
            yield return new StreamingExtendedResponse<string>($"{tag} record {i}");
        }

        throw new StreamingApplicationException("sourceOffline", $"{{\"tag\":\"{tag}\",\"lastIndex\":{recordsBeforeFailure - 1}}}", "Historian connection lost.");
    }

    // The executor's handler throws an unexpected exception mid-stream. Nothing in the application chose to report an
    // error, so the Failed status carries no AppErrCode, and the invoker reports an execution error from the peer. The
    // exchange's completion signal faults with the same error.
    [Fact]
    public async Task StreamFailure_ExecutorUnhandledException()
    {
        const int itemsBeforeFailure = 2;
        LogScenario($"SCENARIO stream failure, execution error: executor's handler throws after {itemsBeforeFailure} items; the invoker sees an execution error from the peer.");
        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        await using ExecutionErrorExecutor executor = new(appContext, executorClient)
        {
            OnStreamingCommandReceived = (requests, _, _) => (ItemsThenCrash(requests, itemsBeforeFailure), new ResponseStreamMetadata()),
            Log = Log,
        };
        await executor.StartAsync();

        await using ExecutionErrorInvoker invoker = new(appContext, invokerClient) { Log = Log };

        var (responses, exchange) = await invoker.InvokeStreamingCommandAsync(OneRequest("browse ns=2"));

        List<string> received = new();
        AkriMqttException error = await Assert.ThrowsAsync<AkriMqttException>(async () =>
        {
            await foreach (StreamingExtendedResponse<string> response in responses.Entries)
            {
                received.Add(response.Payload);
            }
        });

        Log($"invoker: received {received.Count} item(s), then {error.Kind} (remote={error.IsRemote}): {error.Message}");

        Assert.Equal(new[] { "node 0", "node 1" }, received);
        Assert.Equal(AkriMqttErrorKind.ExecutionException, error.Kind);
        Assert.True(error.IsRemote);
        Assert.Equal("Address space read failed.", error.Message);

        AkriMqttException completionError = await Assert.ThrowsAsync<AkriMqttException>(() => exchange.Completion);
        Assert.Same(error, completionError);
    }

    private static async IAsyncEnumerable<StreamingExtendedResponse<string>> ItemsThenCrash(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests, int itemsBeforeFailure)
    {
        await foreach (ReceivedStreamingExtendedRequest<string> _ in requests.Entries)
        {
        }

        for (int i = 0; i < itemsBeforeFailure; i++)
        {
            yield return new StreamingExtendedResponse<string>($"node {i}");
        }

        throw new InvalidOperationException("Address space read failed.");
    }

    // The invoker's own request stream throws after its first entry. The invoker ends the request stream with a Failed
    // status and reports the original error locally; the executor's request stream ends with an execution error from the
    // peer. Failed is not answered, so the executor's handler failing in turn sends nothing back.
    [Fact]
    public async Task StreamFailure_InvokerRequestStreamThrows()
    {
        LogScenario("SCENARIO stream failure, request side: the invoker's request stream throws after one entry; the executor's request stream ends with an execution error from the peer.");
        ApplicationContext appContext = new();
        await using MqttSessionClient executorClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MqttSessionClient invokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();

        TaskCompletionSource<Exception> executorSaw = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> executorReceived = new();

        await using RequestFailureExecutor executor = new(appContext, executorClient)
        {
            OnStreamingCommandReceived = (requests, _, _) => (EchoUntilRequestStreamFails(requests, executorReceived, executorSaw), new ResponseStreamMetadata()),
            Log = Log,
        };
        await executor.StartAsync();

        await using RequestFailureInvoker invoker = new(appContext, invokerClient) { Log = Log };

        var (responses, exchange) = await invoker.InvokeStreamingCommandAsync(OneRequestThenThrow("chunk 0", "Upload source closed."));

        InvalidOperationException localError = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (StreamingExtendedResponse<string> _ in responses.Entries)
            {
            }
        });
        Assert.Equal("Upload source closed.", localError.Message);
        Assert.Same(localError, await Assert.ThrowsAsync<InvalidOperationException>(() => exchange.Completion));

        Exception remoteError = await executorSaw.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Log($"executor: received [{string.Join(", ", executorReceived)}], then {remoteError.GetType().Name}: {remoteError.Message}");

        Assert.Equal(new[] { "chunk 0" }, executorReceived);
        AkriMqttException executionError = Assert.IsType<AkriMqttException>(remoteError);
        Assert.Equal(AkriMqttErrorKind.ExecutionException, executionError.Kind);
        Assert.True(executionError.IsRemote);
        Assert.Equal("Upload source closed.", executionError.Message);
    }

    private static async IAsyncEnumerable<StreamingExtendedResponse<string>> EchoUntilRequestStreamFails(
        IStreamContext<ReceivedStreamingExtendedRequest<string>> requests, List<string> received, TaskCompletionSource<Exception> saw)
    {
        // A yield can't sit inside a try with a catch, so the request stream is advanced by hand to observe its error.
        await using IAsyncEnumerator<ReceivedStreamingExtendedRequest<string>> enumerator = requests.Entries.GetAsyncEnumerator();
        while (true)
        {
            bool moved;
            try
            {
                moved = await enumerator.MoveNextAsync();
            }
            catch (Exception ex)
            {
                saw.TrySetResult(ex);
                throw;
            }

            if (!moved)
            {
                saw.TrySetException(new InvalidOperationException("The request stream ended without an error."));
                yield break;
            }

            received.Add(enumerator.Current.Payload);
            yield return new StreamingExtendedResponse<string>($"ack {enumerator.Current.Payload}");
        }
    }

    private static async IAsyncEnumerable<StreamingExtendedRequest<string>> OneRequest(string payload)
    {
        await Task.Yield();
        yield return new StreamingExtendedRequest<string>(payload);
    }

    private static async IAsyncEnumerable<StreamingExtendedRequest<string>> OneRequestThenThrow(string payload, string error)
    {
        await Task.Yield();
        yield return new StreamingExtendedRequest<string>(payload);

        // Give the first entry time to reach the executor before the stream fails.
        await Task.Delay(200);
        throw new InvalidOperationException(error);
    }
}
