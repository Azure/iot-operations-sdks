# ADR 25: RPC Streaming

## Context

Users have expressed a desire to allow more than one request and/or more than one response per RPC invocation.

## Requirements

 - A single command invocation MUST support an arbitrary number of command requests and responses.
   - A producer MUST be able to send its first request or response without knowing the total number of entries in advance.
   - A stream MAY contain exactly one entry.
 - When exposed to the user, each request and response MUST include the index of its position within the stream.
 - The protocol MUST support multiple independent streaming commands simultaneously.
 - The invoker and the executor MUST each be able to cancel the streamed request and/or the streamed response at any time.
 - The invoker and the executor MUST be able to send their requests and responses at arbitrary times relative to one another, except that the invoker MUST initiate the streaming invocation with a request.
   - For instance, the executor MAY send a response as soon as it receives a request, or it MAY wait until the request stream ends before sending its first response.
   - Likewise, the invoker MAY send a request in reaction to a received response.
 - The invoker and the executor MUST each be able to end their own stream gracefully at any time.
   - A side that does not know in advance whether an entry will be its last MUST still be able to end its stream afterward without sending another full entry.

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

### Conceptual model

The model is defined here language-agnostically; the [appendix](#illustrative-net-api) gives a concrete C# sketch.

While RPC streaming shares a lot with normal RPC, we define a new communication pattern with two roles — a **streaming command invoker** and a **streaming command executor** — analogous to the existing command invoker and executor.

#### Stream entries and metadata scopes

Each entry in a request or response stream pairs a user **payload** with **metadata**. That metadata combines two scopes:

- **Message metadata** is scoped to the individual stream entry it travels with.
- **Stream metadata** applies to a whole stream. The request and response streams carry **different** stream metadata, so this scope is **asymmetric** across directions. On the wire it repeats on every message to survive first-message loss, and is read once.

#### Core abstractions

Each side both produces and consumes a stream — the invoker produces requests and consumes responses, the executor does the reverse — and the two together form one **exchange**, the unit of cancellation and timeout.

The streams themselves surface as plain async sequences — you **supply** the one you produce and **iterate** the one you consume — so producing and consuming take different shapes. Completion, cancellation, and timeout are **exchange-scoped** — one per invocation, not per direction — so a single cancel or timeout covers the whole exchange rather than either individual stream (see [cancellation](#cancellation-support) and [timeout](#timeout-support)).

#### Exchange completion

Each producer ends its own stream with an **`isLast`** signal. An exchange is **gracefully complete** only when *both* of its streams have closed: the invoker has sent its `isLast` request **and** received the `isLast` response, and symmetrically the executor has received the `isLast` request **and** sent the `isLast` response. Closing one stream (via `isLast`) does **not** end the exchange — a side that finishes its own stream early stays active until the other stream closes too, or until the [exchange timeout](#exchange-level-timeout) fires. Any other terminal — error, cancellation, or timeout — ends the whole exchange immediately. Requiring both streams to close is the shared definition of completion used by [timeout](#timeout-support) and [cancellation](#cancellation-support).

#### Invoker behavior

The invoker supplies the outbound **request stream** (an async sequence of request entries) together with that stream's metadata; it must contain at least one entry. The invocation establishes the exchange; it does **not** represent completion of the request stream. The invoker activates response reception, sends the mandatory first request, and then returns the inbound **response stream** — without waiting for the second request or for the request stream to end.

After returning, both streams proceed concurrently, so each can react to the other. The response stream exposes response data and metadata; completion, cancellation, and timeout operate at the exchange scope.

An empty request stream or setup error fails the invocation before an exchange is returned. Any later request-sending error terminates the local exchange, stops request publication, and triggers a best-effort cancellation; it is surfaced through the exchange's completion signal and, while still open, the response stream.

#### Executor behavior

The streaming command executor receives the inbound **request stream** and that request stream's metadata, returns the outbound **response stream** (an async sequence of response entries) together with that stream's metadata, and can cancel or observe the exchange's completion and timeout.

### MQTT layer protocol

#### Streaming user property

To convey streaming context, each message carries a `__stream` MQTT user property with the value:

```txt
<property_value> ::= <stream_entity_metadata> | <stream_control_metadata> | <stream_status_metadata>
<stream_entity_metadata>  ::= "d" ":" <message_index> [ ":" <timeout_length> ]
<stream_control_metadata> ::= "c" ":" <message_index> ":" <control_command_word> [ ":" <timeout_length> ]
<stream_status_metadata>  ::= "s" ":" <message_index> [ ":" <timeout_length> ]
<message_index> ::= <uint>
<timeout_length> ::= <uint>
<control_command_word> ::= "cancel" | "last"
```

**Table 1. `__stream` value fields.** The value takes one of three mutually exclusive forms distinguished by a leading tag — a **data** form (`d:…`) for stream entries, a **control** form (`c:…`) for stream control, and a **status** form (`s:…`) for reporting an outcome about a received message. Because the form is tagged, a message only ever carries the fields that apply to it; there are no fields to ignore.

| Field                  | Type               | Form    | Meaning                                                                                                                                                                          |
| ---------------------- | ------------------ | ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `d` (tag)              | literal            | data    | Identifies a **data** message that carries one stream entry                                                                                                                     |
| `message_index`        | uint               | data    | Position of this message within the producer's stream; data and control share one counter                                                                                      |
| `timeout_length`       | uint               | data    | Invoker's current timeout counter (exchange time remaining), in seconds; **request-direction only** (invoker → executor), omitted on response-direction messages |
| `c` (tag)              | literal            | control | Identifies a **control** message                                                                                                                                                |
| `message_index`        | uint               | control | Position of this message within the producer's stream; data and control share one counter                                                                                      |
| `timeout_length`       | uint               | control | Invoker's current timeout counter (exchange time remaining), in seconds; **request-direction only** (invoker → executor), omitted on response-direction messages |
| `control_command_word` | `cancel` \| `last` | control | `last`: the standalone final message that closes the producer's stream (no payload or application-provided user properties). `cancel`: a cancellation request for the exchange.  |
| `s` (tag)              | literal            | status  | Identifies a **status** message reporting an outcome about a received message; the outcome details travel in `__stat`                                                           |
| `message_index`        | uint               | status  | Stream index of the **received** message the status refers to (the peer's index, not the producer's counter)                                                                    |
| `timeout_length`       | uint               | status  | Invoker's current timeout counter (exchange time remaining), in seconds; **request-direction only** (invoker → executor), omitted on response-direction messages |

Examples:

- ```d:0:10``` — a request-direction data message at stream index `0`; the invoker's remaining exchange timeout is 10 seconds.
- ```c:4:last:6``` — the request stream's final (`isLast`) message at index `4`; the invoker's remaining exchange timeout has dropped to 6 seconds (the field is a live countdown).
- ```c:2:cancel:10``` — a request-direction cancellation at the producer's next index (`2`).
- ```s:1:10``` — a request-direction status about the received message at stream index `1`; its `__stat` value carries the outcome (intended to communicate non-successful statuses).
- ```d:0``` / ```c:7:last``` / ```s:3``` — the response-direction counterparts (executor → invoker); identical forms except `timeout_length` is omitted.

Every MQTT PUBLISH belonging to a streaming exchange must include `__stream` in exactly one of these three forms: data messages use the `d` form, control messages use the `c` form (`c:…:cancel`, `c:…:last`), and status messages use the `s` form. A status message carries no stream entry; its `__stat` property carries the outcome details for the received message it references.

[see cancellation support](#cancellation-support) and [timeout support](#timeout-support) for how these fields are used.

#### Topics and routing

A single **correlation GUID** identifies the whole exchange; every message of both streams carries it. The exchange uses **two MQTT topics** — the **command topic** (invoker → executor) and the **response topic** (executor → invoker) — and each carries that direction's data, control, and status messages together.

The executor subscribes to the **command topic** with a **shared subscription** (so that, with multiple executors, only one handles each exchange), with the same topic pre/suffixing and custom-topic-token support as vanilla RPC. The **response topic** is `clients/{invoker client id}/...` (prefixed like vanilla RPC), unique to the invoker and not shared; the invoker subscribes to it before publishing. Each request-stream message the invoker publishes carries the response topic (so the executor knows where to reply) and a `$partition` user property set to the invoker's client id.

Because the command topic is a shared subscription, **every** command-topic packet for an exchange — request data, an `isLast` request, invoker cancellation, and the invoker's `Canceled` acknowledgement — must carry that same `$partition`, so the broker routes them all to the same executor; otherwise a control packet could reach a different executor that holds no state for the correlation and be silently dropped. Response-topic packets need only the correlation data, since that topic is already unique to the invoker.

#### Exchange lifetime

Because closing one stream does not end the exchange (see [exchange completion](#exchange-completion)), each endpoint keeps its per-correlation state active until the exchange is terminal, so control still flows after an `isLast`. Once a side is terminal, further data messages for that correlation are acknowledged and ignored; only the required control re-answers (for example, re-sending `Canceled` for a re-issued cancellation) are sent. The per-correlation state is kept as a **tombstone** so late or duplicate packets remain routable and are not treated as a new stream; see [exchange level timeout](#exchange-level-timeout) for how long.

#### Producing and consuming

A side **consumes** one stream and **produces** the other. The consuming and producing rules below are identical for both roles; two further rules apply to the **executor** only.

**Consuming a stream:**

- **De-dup caching.** A consumer de-dups received data messages (QoS 1 may re-deliver) by correlationId + index — the index distinguishes duplicates since the correlationId is shared by the whole stream. Each cache entry is retained for the duration of its message's expiry interval (see [message level timeout](#message-level-timeout)), even beyond the end of the stream: clearing it when the stream finishes would let a late re-delivery still within its expiry window be treated as new, which is unsafe for non-idempotent commands.
- **Acknowledgement.** By default a consumer acknowledges each message as soon as it is delivered to the user. Users may opt into manual acknowledgement to finish processing a message before forgoing broker re-delivery on an unexpected crash. 
- **`isLast` receipt.** On an `isLast` control message (`c:…:last`), the consumer notifies the user that the stream has ended. This standalone message carries no payload or application-provided user properties and is **not** surfaced as a stream entry ([why `isLast` is its own message](#islast-message-being-its-own-message)). Because delivery order is guaranteed, receiving further data for that stream after its `isLast` is a protocol violation.

**Producing a stream:** every data message carries the same correlation data, the appropriate [`__stream` metadata](#streaming-user-property), the serialized user payload, and any message metadata plus the stream metadata, at QoS 1. The producer ends its stream with a standalone `isLast` message (no payload, no application user properties) on the same topic and correlation. Which topic each side uses, and the `$partition` requirement on the command topic, are covered in [topics and routing](#topics-and-routing) above.

**Executor-only rules:**

- If an `isLast` arrives before any data message in the request stream, log an error, acknowledge it, and ignore it — a request stream must have at least one entry.
- Unlike vanilla RPC, the executor keeps **no replay cache**: streams may grow without bound, so replaying a response stream isn't feasible.

### Timeout support

Timeout support avoids either side getting stuck — waiting for a final message that was lost or never sent, or for a peer that has silently stalled (the invoker waiting on responses, or the executor waiting on requests).

#### Approach

The invoker configures an **exchange timeout** — a single total budget for the whole exchange — plus a per-message expiry for request/response data. If the user does not specify one, a configurable default applies; a user-supplied value must be positive and finite and is rounded up to whole seconds. Every exchange therefore has a positive, finite timeout of at least one second.

The exchange timeout bounds **total elapsed time from when the exchange begins**, not inactivity; it caps the whole exchange and is the backstop for one that never reaches [graceful completion](#exchange-completion) — a stream that never closes, a lost final message, or a crashed peer.

##### Exchange level timeout

Each side runs its own countdown from the start of the exchange and does **not** reset it:

- The **invoker** starts its timer when it sends its first request. If it elapses before [graceful completion](#exchange-completion), it reports the timeout to the user and stops sending.
- The **executor** starts its timer on the first request it receives. If it elapses before [graceful completion](#exchange-completion), it reports the timeout to the user.

Every **request-direction** message (invoker → executor) carries the **current invoker timeout counter value** in the `timeout_length` field of `__stream` (in seconds), repeated on each so the timeout survives loss of earlier messages and lets a different executor recover the exchange mid-stream; response-direction messages (executor → invoker) omit it. Seconds align with the MQTT message expiry interval used for other timeouts, keep the header small for long-running streams, and avoid implying a sub-second precision that isn't meaningful.

A local timeout terminates only that side's exchange state; neither side sends a timeout status to the other. The peer reaches its own timeout independently if no further progress occurs.

Messages received by either side after it has timed out are acknowledged but otherwise ignored. Each party therefore keeps a per-correlation tombstone for timed-out streams so post-timeout packets aren't treated as a new stream; it is retained at least as long as the longest expiry of any packet that could still arrive.

##### Message level timeout

We will allow users to set the message expiry interval of each message in a request/response stream; by default it equals the exchange timeout. Every stream message _must_ include a positive, finite message expiry — a message with no (or zero) expiry is rejected. The receiving end uses this value as the de-dup cache length for the cached message (vanilla RPC has the [same requirement](../../reference/command-timeouts.md#input-values)).


#### Alternative timeout designs considered

- An **idle (inactivity) timeout kept alive by heartbeats** — the timer resets on every message received from the peer, and each side emits periodic heartbeats so a live-but-quiet peer keeps it from firing; this supports indefinitely-long streams while still detecting a stalled peer on a short inactivity window.
  - Rejected for now: significantly more complex (heartbeat traffic, per-message resets, extra failure modes) than a single overall budget. Revisit only if a customer explicitly needs an unbounded stream with liveness detection; adding it later alongside the overall timeout would create two clashing timeout semantics.
- The above approach, but trying to calculate time spent on broker side (using message expiry interval) so that invoker and executor timeout at the same exact time
  - This would require additional metadata in the ```__stream``` user property (intended vs received message expiry interval) and is only helpful in the uncommon scenario where a message spends extended periods of time at the broker
- Specify the number of milliseconds allowed between the executor receiving the final command request and delivering the final command response.
  - This is the approach that gRPC takes, but it doesn't account for scenarios where the invoker/executor dies unexpectedly (since gRPC relies on a direct connection between invoker and executor)
- Use the message expiry interval of the first received message in a stream to indicate the exchange timeout
  - Misuses the message expiry interval's purpose and could lead to the broker storing messages for extended periods of time unintentionally
- Send a terminal timeout status when a local timer expires
  - Both sides already have local timers and terminate independently. A post-timeout status would require a separate delivery budget and could arrive after the peer is already terminal.

### Cancellation support

To avoid scenarios where long-running streaming requests/responses are no longer wanted, either side may cancel a streaming RPC at any time while the exchange is active.

Cancellation requests may include user properties explaining why cancellation was requested.

#### API

Cancellation is an **exchange-scoped** operation — a single cancel per invocation covers the whole exchange, not one per direction or stream — available to both the invoker and the executor.

Either side invokes the **cancel** operation (optionally attaching user properties) and observes peer cancellation or local timeout, along with any user properties on the received cancellation. For a concrete illustration see the [appendix](#illustrative-net-api); for detailed examples see the [integration tests](../../../dotnet/test/Azure.Iot.Operations.Protocol.IntegrationTests/StreamingIntegrationTests.cs).

#### Canceled status

Cancellation acknowledgements reuse the same status mechanism as vanilla RPC: the status travels in the `__stat` MQTT user property (with an optional human-readable `__stMsg`), not a separate acknowledgement packet. Streaming introduces one new status code:

- **`Canceled` = `499`** (mirrors the conventional "Client Closed Request" code). Cancellation is not an application error, so `__apErr` is `false`.

A `Canceled` response from the executor to the invoker looks like this on the wire:

```text
PUBLISH
  topic:                 clients/{invokerId}/...        # the stream's response topic
  qos:                   1
  correlationData:       <same GUID as the stream>
  messageExpiryInterval: <control-message expiry defined above>
  userProperties:
    __stream:  s:<cancel request's index>   # Canceled status (s form) about the received cancel request; response direction, so no timeout
    __stat:    499                  # Canceled
    __stMsg:   "Canceled"           # optional
    __apErr:   false                # cancellation is not an application error
    __protVer: <streaming protocol version>
    __ts:      <HLC timestamp>
  payload:               <none>
```

When the invoker acknowledges an executor-initiated cancellation on the command topic, it uses the request-direction form `__stream: s:<cancel request's index>:<effective stream timeout seconds>` (request direction, so it also carries the timeout); all other fields have the same meaning. Wherever the sections below refer to the `Canceled` code / status, they mean a message of this shape.

#### Sending a cancellation

Either side cancels by publishing a [`cancel` control message](#streaming-user-property) (`c:…:cancel`), no payload, the same correlation data, on the topic it uses to reach the other party:

- The **invoker** cancels on the command topic, then keeps listening on the response topic and delivering any in-flight responses to the application until the `Canceled` status arrives and closes the channel, or the whole exchange times out.
- The **executor** cancels on the invoker's response topic, then keeps listening on the command topic and delivering any in-flight requests to the application until the `Canceled` status arrives and closes the channel, or the whole exchange times out.

Cancellation is **idempotent**: the sender may issue `cancel` more than once while exchange is active. Receiving `Canceled` confirms cancellation; any other terminal outcome ends re-issuing without confirming it.

#### Receiving a cancellation

The receiver of a cancellation responds depending on the state of that receiver:

- **Still active** — notifies the application, replies with `Canceled` on the appropriate topic.
- **Already completed** (both streams closed) — acknowledges the message and sends nothing.
- **Already canceled** — re-sends `Canceled` so a later (re-issued) cancellation is answered.

### Error handling and stream termination

The **termination machinery** is symmetric across both directions; what is asymmetric is **which statuses each side originates** — inherited from RPC, where the outcome `__stat` is a response-direction concept.

Both produced streams end **gracefully** the same way: a standalone `isLast` message (no payload or application-provided user properties, a success status). Either direction can also end with the **`Canceled`** terminal that the [cancellation](#cancellation-support) mechanism produces. The directions differ only in their **error** ending:

- The **response stream** carries a `__stat` on every message and can self-terminate on error. A successful entry uses `200` when it carries a payload and `204` when it does not; neither terminates the stream. An **error status (`4xx`/`5xx`) is self-terminating**: the executor sends nothing further, so the receiver surfaces it as the terminal error and ends the response stream. An error response does **not** also need a separate `isLast` message — its status is sufficient, and the executor may be unable to send a separate `isLast` (for example, after a crash). This covers executor exceptions (`500`) and request/protocol validation errors (`4xx`).
- The **request stream** carries no outcome `__stat`, so it has no self-terminating-error form. A request-side failure — the request pump throwing, or the application abandoning the exchange — instead terminates the exchange through a best-effort **cancellation** (see [invoker behavior](#invoker-behavior)).

Whichever side originates it, a terminal status is **exchange-scoped**, not a stream entry, and is de-duplicated using exchange terminal state keyed by correlation data rather than by index. Because it is exchange-scoped, it may arrive **after** a graceful `isLast` has already closed the data stream in its direction — for example an executor error raised while the request stream is still open, or a `Canceled` after the request `isLast`. Such a status does not reopen the data stream; it terminates the still-active **exchange**. If the corresponding iterator is still open the status faults it; if the iterator already completed via `isLast`, the status is observed only through the exchange's completion signal.

The `__apErr` (`IsApplicationError`) property classifies an error as either a framework/protocol error (`__apErr = false`: canceled, bad request, internal error) or an application-level error (`__apErr = true`) the command logic chose to return. **Either way the error status terminates the stream** — there is no per-message error status that leaves the stream running. An application that needs a per-item outcome while the stream keeps going (for example, a batch where individual items may fail) must encode that in its response payload (`TResp`), not the protocol status — a mid-stream "failed item" is just a normal response whose payload represents the failure.

### Disconnection scenario considerations

In every case, QoS 1 sessions carry queued messages across a reconnection (within each message's expiry), and whichever side stops seeing progress reaches its own local [exchange timeout](#exchange-level-timeout) independently.

- Invoker side disconnects unexpectedly while sending requests
  - On reconnection, the request messages queued in its session client publish as expected and the exchange resumes
  - Otherwise the executor stops seeing requests and times out
- Invoker side disconnects unexpectedly while receiving responses
  - The broker holds each published response for its [message-level timeout](#message-level-timeout) (message expiry interval) and redelivers those still within their expiry on reconnection; those whose expiry lapses first are lost
  - If the invoker's session is lost, the exchange cannot resume and the executor times out
- Executor side isn't connected when the invoker sends the first request
  - The broker may return a "no matching subscribers" PUBACK; whether to retry here is TBD
  - On a success PUBACK the request is held for its message expiry, and the invoker times out if no executor consumes it in time
- Executor side disconnects unexpectedly while receiving requests
  - The broker holds each published request for its [message-level timeout](#message-level-timeout) (message expiry interval) and redelivers those still within their expiry on reconnection; those whose expiry lapses first are lost
  - If the executor's session is lost, the invoker times out
- Executor side disconnects unexpectedly while sending responses
  - On reconnection, the response messages queued in its session client publish as expected and the exchange resumes
  - Otherwise the invoker stops seeing responses and times out

### Protocol versioning

By maintaining RPC streaming as a separate communication pattern from normal RPC, we introduce an independent protocol version for RPC streaming. It starts at ```1.0``` and follows the same protocol versioning rules as telemetry and normal RPC.

## Alternative designs considered

 - Allow the command executor to decide at run time of each command if it will stream responses independent of the command invoker's request
   - This would force users to always call the ```InvokeCommandWithStreaming``` API on the command invoker side, and that returned object isn't as easy to use for single responses
 - Treat streaming RPC as the same protocol as RPC
   - This introduces error cases such as: an invoker that thinks a method is non-streaming while the executor tries streaming responses; or an executor that receives a streaming command but has no streaming handler set (which must be optional, since not every executor has streaming commands)
   - The API is messy because an invoker/executor should not expose streaming APIs if it has no streaming commands
   - The caching behavior of normal RPC doesn't fit streamed RPCs, which may grow indefinitely large

## Appendix

### Illustrative .NET API

The following C# sketches one possible implementation of the [conceptual model](#conceptual-model) above. It is illustrative only — the Rust and Go implementations will expose equivalent shapes idiomatically.

Two base classes define the pattern — `StreamingCommandInvoker` and `StreamingCommandExecutor` — reusing "extended" request/response types that pair each payload with its message metadata:

```csharp
public class StreamingExtendedRequest<TReq>
    where TReq : class
{
    public TReq Payload { get; set; }
    public StreamMessageMetadata Metadata { get; set; }
    // Per-message MQTT expiry; defaults to the exchange timeout and must be <= it.
    public TimeSpan? MessageExpiry { get; set; }
}

public class StreamingExtendedResponse<TResp>
    where TResp : class
{
    public TResp Payload { get; set; }
    public StreamMessageMetadata Metadata { get; set; }
    public TimeSpan? MessageExpiry { get; set; }
}

// Stream index, HLC timestamp, and per-message user properties.
public class StreamMessageMetadata
{
    public uint Index { get; init; }
    public HybridLogicalClock? Timestamp { get; init; }
    public Dictionary<string, string> UserData { get; init; } = new();
}

// Consumed entries add manual acknowledgement (used when auto-ack is off).
public class ReceivedStreamingExtendedRequest<TReq> : StreamingExtendedRequest<TReq>
    where TReq : class
{
    // Once-only; acks are sent in order and count against the client's Receive Maximum.
    public Task AcknowledgeAsync() { ... }
}

public class ReceivedStreamingExtendedResponse<TResp> : StreamingExtendedResponse<TResp>
    where TResp : class
{
    public Task AcknowledgeAsync() { ... }
}

// Stream metadata is asymmetric, mirroring vanilla RPC's request/response metadata.
public class RequestStreamMetadata
{
    ...
}

public class ResponseStreamMetadata
{
    ...
}
```

A consumed stream is just an `IAsyncEnumerable<T>` of entries (stream metadata delivered separately as `RequestStreamMetadata` / `ResponseStreamMetadata`), except the invoker's response stream — `IResponseStream<T>` wraps the entries so its metadata can be **awaited**, since the invoker returns before the first response. The **exchange handle** carries per-exchange completion, cancellation, and timeout:

```csharp
public interface IResponseStream<T>
    where T : class
{
    IAsyncEnumerable<T> Entries { get; set; }

    // Faults if the exchange ends before any response arrives.
    Task<ResponseStreamMetadata> StreamMetadata { get; }
}

public interface IExchangeHandle
{
    // Completes on graceful close; faults or cancels on any other terminal.
    Task Completion { get; }

    Task CancelAsync(Dictionary<string, string>? userProperties = null, CancellationToken cancellationToken = default);

    // Fires on peer cancel or timeout; use IsCanceled / HasTimedOut to distinguish.
    CancellationToken CancellationToken { get; }

    bool IsCanceled { get; }

    bool HasTimedOut { get; }

    Dictionary<string, string>? GetCancellationRequestUserProperties();
}
```

The invoker supplies the request stream (and its stream-level metadata) and returns the response stream plus the exchange handle:

```csharp
public abstract class StreamingCommandInvoker<TReq, TResp>
    where TReq : class
    where TResp : class
{
    // false -> the caller must ack each response entry via ReceivedStreamingExtendedResponse.AcknowledgeAsync.
    public bool AutomaticallyAcknowledgeResponses { get; set; } = true;

    // Returns after the first request is accepted, without waiting for the rest.
    // exchangeTimeout: total budget for the whole exchange (a configurable default applies if unset).
    public async Task<(IResponseStream<ReceivedStreamingExtendedResponse<TResp>> Responses, IExchangeHandle Exchange)> InvokeStreamingCommandAsync(
      IAsyncEnumerable<StreamingExtendedRequest<TReq>> requests,
      RequestStreamMetadata? streamMetadata = null,
      Dictionary<string, string>? additionalTopicTokenMap = null,
      TimeSpan? exchangeTimeout = default,
      CancellationToken cancellationToken = default) {...}
}
```

The executor's callback receives the request stream, its stream-level metadata, and the exchange handle, and returns the response stream together with its metadata:

```csharp
public abstract class StreamingCommandExecutor<TReq, TResp> : IAsyncDisposable
    where TReq : class
    where TResp : class
{
    public required Func<
        IAsyncEnumerable<ReceivedStreamingExtendedRequest<TReq>>,
        RequestStreamMetadata,
        IExchangeHandle,
        (IAsyncEnumerable<StreamingExtendedResponse<TResp>> Responses, ResponseStreamMetadata Metadata)> OnStreamingCommandReceived { get; set; }

    // false -> the callback must ack each request entry manually.
    public bool AutomaticallyAcknowledgeRequests { get; set; } = true;
}
```

### IsLast message being its own message

Three approaches to marking the final message in a stream were considered, and this is why the other two approaches don't work:

- Carry the final-message marker on a message that also carries a fully-fledged stream entry (a user payload and/or user properties).
  - We must support ending a stream at an arbitrary time even when a fully-fledged message can't be sent, and this approach doesn't allow that.
- Allow the final-message marker on either a fully-fledged message or a standalone message with no user payload or application-provided user properties.
  - This doesn't let the receiving end distinguish "the stream is over" from "this is the final message in the stream" when the user provides no payload or user properties on streamed messages.

Because both either fail our requirements or are ambiguous in corner cases, the final-message marker is its own **standalone** `last` control message (`c:…:last`) with no user payload or application-provided user properties.
