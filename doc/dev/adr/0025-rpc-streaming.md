# ADR 25: RPC Streaming

## Context

Users have expressed a desire to allow more than one request and/or more than one response per RPC invocation.

## Requirements

 - Allow for an arbitrary number of command requests and responses for a single command invocation
   - The total number of requests and responses does not need to be known before the first request/response is sent 
   - The total number of entries in a stream is allowed to be 1
 - When exposed to the user, each request and response includes an index of where it was in the stream
 - Allow for multiple separate commands to be streamed simultaneously
 - Allow for invoker and/or executor to cancel a streamed request and/or streamed response at any time
 - Allow for invoker + executor to send their requests/responses at arbitrary* times
   - For instance, executor may send 1 response upon receiving 1 request (stream message), or it may wait for the request stream to finish before sending the first response
   - Alternatively, this allows the invoker to send a request upon receiving a response
   - *The only limitation is that the invoker must initiate the RPC streaming with a request
 - Allow for invoker/executor to end their own request/response stream gracefully at any time
   - For instance, if the executor doesn't know if a response will be the last one prior to sending it, the executor should still be capable of ending the response stream later without sending another fully-fledged payload

## Non-requirements

 - Different payload shapes per command response/request
 - The API of the receiving side of a stream will provide the user the streamed requests/responses in their **intended** order rather than their **received** order
   - If the stream's Nth message is lost due to message expiry (or other circumstances), our API should still notify the user when the N+1th stream message is received 
   - This may be added as a feature later if requested by customers

## State of the art

gRPC supports these patterns for RPC:
- [Unary RPC](https://grpc.io/docs/what-is-grpc/core-concepts/#unary-rpc) (1 request message, 1 response message)
- [Server streaming RPC](https://grpc.io/docs/what-is-grpc/core-concepts/#server-streaming-rpc) (1 request message, many response messages)
- [Client streaming RPC](https://grpc.io/docs/what-is-grpc/core-concepts/#client-streaming-rpc) (many request messages, one response message)
- [Bi-directional streaming RPC](https://grpc.io/docs/what-is-grpc/core-concepts/#bidirectional-streaming-rpc) (many request messages, many response messages. Request and response stream may send concurrently and/or in any order)

[gRPC also allows for either the client or server to cancel an RPC at any time](https://grpc.io/docs/what-is-grpc/core-concepts/#cancelling-an-rpc)

## Decision

### API design, .NET

While RPC streaming shares a lot of similarities to normal RPC, we will define a new communication pattern to handle this scenario with two corresponding base classes: ```StreamingCommandInvoker``` and ```StreamingCommandExecutor```. 

These new base classes will use similar versions of the ```ExtendedRequest``` and ```ExtendedResponse``` RPC classes to include the streaming-specific information about each request and response:

```csharp
public class StreamingExtendedRequest<TReq>
    where TReq : class
{
    public TReq Request { get; set; }
    public StreamMessageMetadata Metadata { get; set; }
}

public class StreamingExtendedResponse<TResp>
    where TResp : class
{
    public TResp Response { get; set; }
    public StreamMessageMetadata Metadata { get; set; }
}
```

#### Invoker side

The new API will ask users to provide a stream of request payloads + metadata, any stream-level metadata and timeout/cancellation tokens. It returns an `IStreamContext` that is itself the async-enumerable stream of responses and also exposes cancellation, so the user can iterate the responses and terminate the stream exchange at any time from the same object. 

```csharp
public abstract class StreamingCommandInvoker<TReq, TResp>
    where TReq : class
    where TResp : class
{
    // Invoke a streaming command. requests must contain at least one entry.
    // Signalling cancellationToken also makes a single attempt to notify the executor.
    public async Task<IStreamContext<StreamingExtendedResponse<TResp?>>> InvokeStreamingCommandAsync(
      IAsyncEnumerable<StreamingExtendedRequest<TReq>> requests,
      StreamRequestMetadata? streamRequestMetadata = null, 
      Dictionary<string, string>? additionalTopicTokenMap = null, 
      TimeSpan? commandTimeout = default, 
      CancellationToken cancellationToken = default) {...}
}
```

#### Executor side

The new ```StreamingCommandExecutor``` will largely look like the existing ```CommandExecutor```, but the callback to notify users that a command was received will include a stream of requests and return a stream of responses.

```csharp
public abstract class StreamingCommandExecutor<TReq, TResp> : IAsyncDisposable
    where TReq : class
    where TResp : class
{
    // Invoked per streaming command: receives the request stream, returns the response stream.
    public required Func<IStreamContext<StreamingExtendedRequest<TReq?>>, StreamRequestMetadata, CancellationToken, IAsyncEnumerable<StreamingExtendedResponse<TResp>>> OnStreamingCommandReceived { get; set; }
}

```

With this design, commands that use streaming are defined at codegen time. Codegen layer changes will be defined in a separate ADR, though.

### MQTT layer protocol

#### Streaming user property

To convey streaming context in a request/response stream, we will put this information in the "__stream" MQTT user property with a value that looks like:

```<index>:<isLast>:<cancelRequest>:<stream timeout milliseconds>```

with data types

```<uint>:<boolean>:<boolean>:<uint>```

where the field ```:<stream timeout milliseconds>``` is only present in request stream messages and may be omitted if the stream has no timeout.

**Table 1. `__stream` fields, in the order they appear in the value.**

| Field | Type | Present in | Meaning | Ignored when |
|-------|------|------------|---------|--------------|
| `index` | uint | every stream message | Position of this message within the stream | `cancelRequest = true` |
| `isLast` | bool | every stream message | `true` on the standalone final message that closes the stream (no payload, no user properties) | `cancelRequest = true` |
| `cancelRequest` | bool | every stream message | `true` marks a cancellation request for the stream | — (always meaningful) |
| `stream timeout ms` | uint | **request** stream messages only | Stream-level timeout countdown value in milliseconds | Omitted entirely when the stream has no timeout; never present on response messages |

For example:

```0:false:false:10000```: The first (and not last) message in a request stream where the RPC should timeout beyond 10 seconds

```3:true:false```: The third and final message in a response stream

```0:true:false:1000```: The first and final message in a request stream where the RPC should timeout beyond 1 second

```0:true:true:0```: This request stream has been canceled. Note that the values for ```index```, ```isLast```, and ```<stream timeout milliseconds>``` are ignored here.

```0:true:true```: This response stream has been canceled. Note that the values for ```index``` and ```isLast``` are ignored here.

[see cancellation support for more details on cancellation scenarios](#cancellation-support)

[see timeout support for more details on timeout scenarios](#timeout-support)

#### Invoker side

The streaming command invoker will first subscribe to the appropriate response topic prior to sending any requests

Once the user invokes a streaming command, the streaming command invoker will send one to many MQTT messages with:
  - The same response topic
    - This response topic must be prefixed with 'clients/{mqtt client id of invoker}' like in vanilla RPC
  - The same correlation data
  - The user property "$partition" set to a value of the client Id of the MQTT client sending this invocation
    - This ensures that the broker always routes the messages in the stream to the same executor
  - The appropriate streaming metadata [see above](#streaming-user-property)
  - The serialized payload as provided by the user's request object
  - Any user-definied metadata as specified in the ```ExtendedStreamingRequest```
  - QoS 1

Once the stream of requests has started sending, the streaming command invoker should expect the stream of responses to arrive on the provided response topic with the provided correlation data and the streaming user property.

Once the user-supplied stream of request messages has ended, the streaming command invoker should send one final message to the same topic/with the same correlation data with no payload and with the 'isLast' flag set in the '__stream' metadata bundle.  

Upon receiving an MQTT message in the response stream with the 'isLast' flag set in the '__stream' metadata, the streaming command invoker should notify the user that the stream of responses has ended. This particular message should not contain any payload or other user properties, so the message _should not_ be propagated to the user as if it were part of the response stream. [See here for more details on why this ```isLast``` flag is an independent message](#islast-message-being-its-own-message).

By default, the streaming command invoker will acknowledge all request messages it receives as soon as they are given to the user. Users may opt into manual acknowledgements, though. Opting into manual acknowledgements allows the user time to "process" each response as necessary before forgoing re-delivery from the broker if the invoker crashes unexpectedly.

The streaming command invoker will provide de-dupe caching of received responses to account for QoS 1 messages potentially being re-delivered. The streaming command invoker will de-dup using a combination of the correlationId of the stream and the index of the message within that stream (the index is what distinguishes duplicates, since the correlationId is shared by every message in the stream). Each de-dup cache entry must be retained for the duration of its message's expiry interval (see [message level timeout](#message-level-timeout)), even when that extends beyond the end of the stream. Clearing entries the moment the stream finishes would let a late QoS 1 re-delivery (one still within its expiry window) be treated as new, which is unsafe for non-idempotent commands.

#### Executor side

A streaming command executor should start by subscribing to the expected command topic
  - Even though the streaming command classes are separate from the existing RPC classes, they should also offer the same features around topic string pre/suffixing, custom topic token support, etc.
  - The executor should use a shared subscription so that, if there are multiple executors, only one of them receives each stream

Upon receiving a MQTT message that contains a streaming request, the streaming executor should notify the application layer that the first message in a request stream was received. Once the executor has notified the user that the first message in a request stream was received, the user should be able to provide a stream of responses. Upon receiving each response in that stream from the user, the executor will send an MQTT message for each streamed response with:
  - The same correlation data as the original request
  - The topic as specified by the original request's response topic field
  - The appropriate streaming metadata [see above](#streaming-user-property)
  - The serialized payload as provided by the user's response object
  - Any user-definied metadata as specified in the ```ExtendedStreamingResponse```
  - QoS 1

Upon receiving an MQTT message in the request stream with the 'isLast' flag set in the '__stream' metadata, the streaming executor should notify the user that the stream of requests has ended. This particular message should not contain any payload or other user properties, so the message _should not_ be propagated to the user as if it were part of the request stream. [See here for more details on why this ```isLast``` flag is an independent message](#islast-message-being-its-own-message).

If a streaming command executor receives an MQTT message with the 'isLast' flag set but has not received any other messages in that request stream, the executor should log an error, acknowledge the message, but otherwise ignore it. A stream of requests must have at least one entry.

By default, the streaming command executor will acknowledge all response messages it receives as soon as they are given to the user. Users may opt into manual acknowledgements, though. Opting into manual acknowledgements allows the user time to "process" each response as necessary before forgoing re-delivery from the broker if the executor crashes unexpectedly.

Also unlike normal RPC, the streaming command executor will not provide any re-play cache support. This is because streams may grow indefinitely in length and size so re-playing a response stream isn't feasible.

The streaming command executor will provide de-dupe caching of received requests to account for QoS 1 messages potentially being re-delivered. The streaming command executor will de-dup using a combination of the correlationId of the stream and the index of the message within that stream (the index is what distinguishes duplicates, since the correlationId is shared by every message in the stream). Each de-dup cache entry must be retained for the duration of its message's expiry interval (see [message level timeout](#message-level-timeout)), even when that extends beyond the end of the stream. Clearing entries the moment the stream finishes would let a late QoS 1 re-delivery (one still within its expiry window) be re-executed, which is unsafe for non-idempotent commands.

### Timeout support

We need to provide timeout support for our streaming APIs to avoid scenarios such as:

- The invoker side is stuck waiting for the final response in a stream because it was lost or the executor side crashed before sending it.
- The executor side is stuck waiting for the final request in a stream because it was lost or the invoker side crashed before sending it.

#### Decision

We will allow configuration on the invoker's side of a timeout for the stream as a whole and a timeout of each message in the request and/or response stream.

##### Stream level timeout

To enable this, each message in the request stream will include a value in the ```<stream timeout milliseconds>``` portion of the ```__stream``` user property. This header should be sent in all request stream messages in case the first N request messages are lost due to timeout or otherwise.

The invoker side will start a countdown from this value after receiving the first PUBACK that ends with throwing a timeout exception to the user if the final stream response has not been received yet. The invoker should not send any further messages beyond this timeout.

The executor side will start a countdown from this value after receiving the first PUBLISH in the request stream. At the end of the countdown, if the executor has not sent the final response in the response stream, the executor should return the ```timeout``` error code back to the invoker. The executor should also notify the user callback to stop. 

Any request stream or response stream messages that are received by the executor/invoker after they have ended the timeout countdown should be acknowledged but otherwise ignored. This will require both parties to track correlationIds for timed out streams for a period of time beyond the expected end of the RPC so that any post-timeout messages are not treated as initiating a new stream.

If the request stream omits the timeout value in the ```__stream``` user property, the invoker and executor should treat the stream as not having a timeout.

This design does make the invoker start the countdown sooner than the executor, but the time difference is negligible in most circumstances.

##### Message level timeout

We will allow users to set the message expiry interval of each message in a request/response stream. By default, each message expiry interval is set equal to the stream-level timeout value. When the stream has no timeout there is no such default, so the user must supply a per-message message expiry interval explicitly.

Both the invoker and executor stream messages _must_ include a message expiry interval, and it must be a positive, finite value: a message with no (or zero) expiry is rejected, as in vanilla RPC, since an unbounded expiry would make the de-dup cache grow without bound. The receiving end will use this value as the de-dup cache length for each cached message. Vanilla RPC has the same requirement as explained [here](../../reference/command-timeouts.md#input-values).

#### Alternative timeout designs considered

- The above approach, but trying to calculate time spent on broker side (using message expiry interval) so that invoker and executor timeout at the same exact time
  - This would require additional metadata in the ```__stream``` user property (intended vs received message expiry interval) and is only helpful
  in the uncommon scenario where a message spends extended periods of time at the broker
- Specify the number of milliseconds allowed between the executor receiving the final command request and delivering the final command response.
  - This is the approach that gRPC takes, but... 
    - It doesn't account for scenarios where the invoker/executor dies unexpectedly (since gRPC relies on a direct connection between invoker and executor)
- Use the message expiry interval of the first received message in a stream to indicate the stream-level timeout
  - Misuses the message expiry interval's purpose and could lead to broker storing messages for extended periods of time unintentionally

### Cancellation support

To avoid scenarios where long-running streaming requests/responses are no longer wanted, we will want to support cancelling streaming RPC calls. 

Since sending a cancellation request may fail (message expiry on broker side), the SDK API design should allow for the user to repeatedly call "cancel". The cancel operation completes when any one of the following occurs: the other party responds (with the "canceled" status, or a normal terminal response if the exchange had already completed), the stream exchange times out, or the caller's own cancellation token fires. Because the stream-level timeout is optional, the caller's cancellation token is the guaranteed bound for streams that have no timeout. 

Additionally, cancellation requests may include user properties. This allows users to provide additional context on why the cancellation is happening.

#### .NET API design

The proposed cancellation support would come from the return type on the invoker side and the provided type on the executor side:

```csharp
public interface IStreamContext<T> : IAsyncEnumerable<T>
    where T : class
{
    // Cancel the exchange; may be called by either side at any time.
    Task CancelAsync(Dictionary<string, string>? userProperties = null, CancellationToken cancellationToken = default);

    // Fires when the other party cancels or the exchange times out; use IsCanceled / HasTimedOut to distinguish.
    CancellationToken CancellationToken { get; }

    // User properties from a received cancellation, or null if none / not canceled.
    Dictionary<string, string>? GetCancellationRequestUserProperties();

    bool HasTimedOut { get; internal set; }

    bool IsCanceled { get; internal set; }
}
```

With this design, we can cancel a stream from either side at any time and check for received user properties on any received cancellation requests. For detailed examples, see the integration tests written [here](../../../dotnet/test/Azure.Iot.Operations.Protocol.IntegrationTests/StreamingIntegrationTests.cs).

### Protocol layer details

Cancellation acknowledgements reuse the same status mechanism as vanilla RPC: the status travels in the `__stat` MQTT user property (with an optional human-readable `__stMsg`), not as a separate acknowledgement packet. Streaming introduces one new status code for cancellation:

- **`Canceled` = `499`** (mirrors the conventional "Client Closed Request" code). Cancellation is not an application error, so `__apErr` is `false`.

A "Canceled" response therefore looks like this on the wire:

```text
PUBLISH
  topic:                 clients/{invokerId}/...        # the stream's response topic
                         (or the command topic when the invoker answers an executor-initiated cancel)
  qos:                   1
  correlationData:       <same GUID as the stream>
  messageExpiryInterval: <remaining budget, in seconds>
  userProperties:
    __stat:    499                # Canceled
    __stMsg:   "Canceled"         # optional
    __apErr:   false              # cancellation is not an application error
    __protVer: <streaming protocol version>
    __ts:      <HLC timestamp>
  payload:               <none>
```

Wherever the sections below refer to the "Canceled" error code / status, they mean a message of this shape.

#### Invoker side

- The command invoker may cancel a streaming command while streaming the request or receiving the stream of responses by sending an MQTT message with: 
  - The same MQTT topic as the invoked method
  - The same correlation data as the invoked method 
  - Streaming metadata with the ["cancel" flag set](#streaming-user-property)
  - No payload
- The command invoker should still listen on the response topic for a response from the executor which may still contain a successful response (if cancellation was received after the command completed successfully) or a response signalling that cancellation succeeded ("Canceled" error code)

As detailed below, the executor may also cancel the stream at any time. When the invoker receives a cancellation request from the executor for a still-active exchange, the invoker should send an MQTT message with:
 - The same topic as the command itself
 - The same correlation data as the command itself
 - The "Canceled" error code

If the invoker receives an executor cancellation for an exchange it already considers complete (it has sent its final request and received the final response), it should acknowledge the message and send nothing. If it receives a duplicate cancellation for an exchange it has already canceled, it should re-send the "Canceled" status so the executor's retried cancel is answered.

After receiving an acknowledgement from the executor side that the stream has been canceled, any further received messages should be acknowledged but not given to the user.

#### Executor side

Upon receiving an MQTT message with the stream "cancel" flag set to "true" that correlates to an actively executing streaming command, the command executor should:
 - Notify the application layer that that RPC has been canceled if it is still running
 - Send an MQTT message to the appropriate response topic with error code "canceled" to notify the invoker that the RPC has stopped and no further responses will be sent.

If the executor receives a cancellation request for a streaming command that has already completed (it has sent its final response and received the final request), it should acknowledge the message and send nothing. If it receives a duplicate cancellation for a command it has already canceled, it should re-send the "canceled" status so the invoker's retried cancel is answered.

The executor may cancel receiving a stream of requests or cancel sending a stream of responses as well. It does so by sending an MQTT message to the invoker with:
  - The same MQTT topic as command response
  - The same correlation data as the invoked method 
  - Streaming metadata with the ["cancel" flag set](#streaming-user-property)
  - No payload

After sending its cancellation, the executor should listen on the request topic for the invoker's "Canceled" acknowledgement; receiving it (or the exchange timing out, or the caller's cancellation token firing) completes the executor's cancel.

Any received MQTT messages pertaining to a command that was already canceled should still be acknowledged. They should not be given to the user, though.

### Error handling and stream termination

Like vanilla RPC, every stream response carries a `__stat` status. A response stream therefore has two ways to end:

- **Gracefully** — the standalone `isLast` message described above (no payload, no user properties, a success status).
- **With an error** — a response whose `__stat` is an error code (`4xx`/`5xx`).

Successful stream items use a `2xx` status (`200`, or `204` for an empty item) and do **not** terminate the stream. A response with an **error status (`4xx`/`5xx`) is self-terminating**: the executor sends nothing further, so the receiver surfaces it as the terminal error and ends the response stream. An error response does **not** also need the `isLast` flag — its status is sufficient, and the executor may be unable to send a separate `isLast` (for example, after a crash). This rule already covers the `timeout` and `Canceled` (`499`) codes described above, and extends to executor exceptions (`500`) and request/protocol validation errors (`4xx`). Note that the trigger is specifically an *error* status: a non-error non-200 such as `204 No Content` is a normal empty item, not a terminator.

The `__apErr` (`IsApplicationError`) property classifies an error as either a framework/protocol error (`__apErr = false`: timeout, canceled, bad request, internal error) or an application-level error (`__apErr = true`) that the command logic chose to return. **Either way the error status terminates the stream** — there is no per-message error status that leaves the stream running.

If an application needs to report a per-item outcome while the stream keeps going (for example, a batch in which individual items may fail), it must encode that in its response payload (`TResp`), not in the protocol status. This is consistent with the non-requirement that all responses in a stream share one payload shape: a mid-stream "failed item" is just a normal response whose payload represents the failure.

### Disconnection scenario considerations

- Invoker side disconnects unexpectedly while sending requests
  - Upon reconnection, the request messages queued in the session client should send as expected
  - If no reconnection, the streaming RPC will timeout
- Invoker side disconnects unexpectedly while receiving responses
  - The broker should hold all published responses for as long as the invoker's session lives and send them upon reconnection
  - If the invoker's session is lost, then the RPC will timeout
- Executor side isn't connected when invoker sends first request
  - Depending on broker behavior, invoker will receive a "no matching subscribers" puback
    - Seems like a scenario we would want to retry?
  - If the broker returns a successful puback, then the invoker side will eventually time out
- Executor side disconnects unexpectedly while receiving requests
  - Broker should hold all published requests for as long as the executor's session lives and send them upon reconnection
  - If the executor's session is lost, the RPC will timeout
- Executor side disconnects unexpectedly while sending responses
  - Upon reconnection, the response messages queued in the session client should send as expected
  - If no reconnection, the streaming RPC will timeout

### Protocol versioning

By maintaining RPC streaming as a separate communication pattern from normal RPC, we will need to introduce an independent protocol version for RPC streaming. It will start at ```1.0``` and should follow the same protocol versioning rules as the protocol versions used by telemetry and normal RPC.

## Alternative designs considered

 - Allow the command executor to decide at run time of each command if it will stream responses independent of the command invoker's request
   - This would force users to always call the ```InvokeCommandWithStreaming``` API on the command invoker side and that returned object isn't as easy to use for single responses
 - Treat streaming RPC as the same protocol as RPC
   - This introduces a handful of error cases such as:
     - Invoker invokes a method that it thinks is non-streaming, but the executor tries streaming responses
     - Executor receives a streaming command but the user did not set the streaming command handler callback (which must be optional since not every command executor has streaming commands)
   - API design is messy because a command invoker/executor should not expose streaming command APIs if they have no streaming commands
   - Caching behavior of normal RPC doesn't fit well with streamed RPCs which may grow indefinitely large


## Appendix

### IsLast message being its own message

There are three possible approaches to marking the final message in a stream that have been considered. Below are the approaches and the reasons why that approach doesn't work

- Require the ```isLast``` flag to be set on a message that carries a fully-fledged stream message (i.e. has a user-provided payload and/or user properties)
  - We must support ending streams at an arbitrary time even if a fully-fledged stream message can't be sent and this approach doesn't allow for that
- Allow the ```isLast``` flag to be set on either a fully-fledged stream message or as a standalone message with no user payload and no user properties
  - This approach does not allow the receiving end to distinguish between "The stream is over" and "This is the final message in the stream" in cases where the user may provide no payload or user properties on streamed messages.

Because the two above approaches either don't support our requirements or have ambiguities in corner cases, we should require the ```isLast``` flag be set on a standalone message with no uesr payload and no user properties.