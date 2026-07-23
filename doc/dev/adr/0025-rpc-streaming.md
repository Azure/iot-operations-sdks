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

### API design

Described here language-agnostically; the [appendix](#illustrative-net-api) gives a concrete C# sketch. The SDKs target Rust, .NET, and Go.

While RPC streaming shares a lot with normal RPC, we define a new communication pattern with two roles — a **streaming command invoker** and a **streaming command executor** — analogous to the existing command invoker and executor.

#### Stream entries and metadata scopes

Each entry in a request or response stream pairs a user **payload** with **per-message metadata**. Streaming distinguishes two metadata scopes:

- **Per-message metadata** travels with each individual stream entry.
- **Per-stream metadata** applies to a whole stream: the producer attaches it and the consumer reads it. The request and response streams carry **different** per-stream metadata (mirroring vanilla RPC's request vs response metadata), so this scope is **asymmetric** across directions. On the wire it repeats on every message (like the [stream-level timeout](#stream-level-timeout)) to survive first-message loss, and is read once.

#### Core abstractions

Each side both produces and consumes a stream — the invoker produces requests and consumes responses, the executor does the reverse — and the two together form one **exchange**, the unit of cancellation and timeout.

Two abstractions carry these across the API:

- A **stream context** is the **consume** side of one stream — the async sequence of entries you receive. A stream you **produce** is supplied directly as an async sequence together with its per-stream metadata, so producing and consuming use different shapes.
- An **exchange context** is the per-exchange lifecycle and control handle for completion, cancellation, and timeout. Being **exchange-scoped** (one per invocation, not per direction), it lives here rather than on the *stream context*, so a single cancel/timeout covers the whole invocation.

An exchange is **gracefully complete** only when *both* of its half-streams have closed: the invoker has sent its `isLast` request **and** received the `isLast` response, and symmetrically the executor has received the `isLast` request **and** sent the `isLast` response. Closing one half (via `isLast`) does **not** end the exchange — a side that finishes its own stream early stays active for the other half until it closes too, or until the [idle timeout](#stream-level-timeout) fires. Any other terminal — error, cancellation, or timeout — ends the whole exchange immediately. This both-halves condition is the shared definition of completion used by [timeout](#timeout-support) and [cancellation](#cancellation-support).

#### Invoker side

The invoker supplies the outbound **request stream** (an async sequence of request entries) together with that stream's metadata; it must contain at least one entry. The invocation establishes the exchange; it does **not** represent completion of the request stream. The SDK activates response reception, sends the mandatory first request, and then returns the inbound **response stream** together with the *exchange context* — without waiting for the second request or for the request stream to end. Early responses are buffered for iteration.

After returning, both streams proceed concurrently, so each can react to the other. The response stream exposes response data and metadata; the exchange context exposes lifecycle and control.

An empty request stream or setup error fails the invocation before an exchange is returned. Any later request-sending error terminates the local exchange, stops request publication, and triggers a best-effort cancellation; it is exposed through the exchange context's completion signal and, while still open, the response stream.

#### Executor side

The streaming command executor's callback notifies the user that a command was received; it takes the inbound **request stream** (a *stream context*), that request stream's metadata, and the *exchange context*, and returns the outbound **response stream** (an async sequence of response entries) together with that stream's metadata.

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
| `timeout_length`       | uint               | data    | Effective idle-timeout window, in seconds; **request-direction only** (invoker → executor), omitted on response-direction messages |
| `c` (tag)              | literal            | control | Identifies a **control** message                                                                                                                                                |
| `message_index`        | uint               | control | Position of this message within the producer's stream; data and control share one counter                                                                                      |
| `timeout_length`       | uint               | control | Effective idle-timeout window, in seconds; **request-direction only** (invoker → executor), omitted on response-direction messages |
| `control_command_word` | `cancel` \| `last` | control | `last`: the standalone final message that closes the producer's stream (no payload or application-provided user properties). `cancel`: a cancellation request for the exchange.  |
| `s` (tag)              | literal            | status  | Identifies a **status** message reporting an outcome about a received message; the outcome details travel in `__stat`                                                           |
| `message_index`        | uint               | status  | Stream index of the **received** message the status refers to (the peer's index, not the producer's counter)                                                                    |
| `timeout_length`       | uint               | status  | Effective idle-timeout window, in seconds; **request-direction only** (invoker → executor), omitted on response-direction messages |

Examples:

- ```d:0:10``` — a request-direction data message at stream index `0`; the effective idle timeout is 10 seconds.
- ```c:4:last:10``` — the request stream's final (`isLast`) message; index `4` from the shared data/control counter, 10-second idle timeout.
- ```c:2:cancel:10``` — a request-direction cancellation at the producer's next index (`2`).
- ```s:1:10``` — a request-direction status about the received message at stream index `1`; its `__stat` value carries the outcome (intended to communicate non-successful statuses).
- ```d:0``` / ```c:7:last``` / ```s:3``` — the response-direction counterparts (executor → invoker); identical forms except `timeout_length` is omitted.

Every MQTT PUBLISH belonging to a streaming exchange must include `__stream` in exactly one of these three forms: data messages use the `d` form, control messages use the `c` form (`c:…:cancel`, `c:…:last`), and status messages use the `s` form. A status message carries no stream entry; its `__stat` property carries the outcome details for the received message it references. Only **request-direction** messages (invoker → executor) carry the stream-level `timeout_length`, repeated on each so the timeout survives loss of earlier messages and lets a different executor recover the exchange mid-stream; **response-direction** messages (executor → invoker) omit it, since the invoker already set the timeout and cannot be recovered mid-stream.

[see cancellation support](#cancellation-support) and [timeout support](#timeout-support) for how these fields are used.

#### Exchange routing and lifetime

A single **correlation GUID** identifies the whole exchange — both streams carry it. The exchange spans **two directional routes** — the command topic (invoker → executor) and the response topic (executor → invoker) — each multiplexing a **data lane** and a **control lane**. Data lanes close **independently**, each by its own `isLast`; the control lanes are exchange-scoped, so an `isLast` closes only its direction's data lane and tears down nothing. Both routes and their per-correlation state stay active until the **exchange** is terminal, so control still flows after a half-stream's `isLast`.

Because the executor subscribes to the command topic with a shared subscription, **every** command-topic packet for an exchange — request data, an `isLast` request, invoker cancellation, and the invoker's `Canceled` acknowledgement — must carry the same `$partition` value (the invoker's client id). Otherwise the broker may route a control packet to a different executor that holds no state for the correlation, silently dropping it from the exchange. Response-topic packets need only the correlation data, because `clients/{invokerId}/...` is unique to the invoker and is not a shared subscription.

Once a side has reached a terminal state, further data messages for that correlation are acknowledged and ignored; only the required control re-answers (for example, re-sending `Canceled` for a retried cancellation) are sent. The per-correlation exchange state is kept as a tombstone so that late or duplicate packets remain routable and are not treated as a new stream; see [stream level timeout](#stream-level-timeout) for how long.

#### Common stream handling

A side **consumes** one stream and **produces** the other. The rules below are identical for both roles.

**Consuming a stream:**

- **De-dup caching.** A consumer de-dups received data messages (QoS 1 may re-deliver) by correlationId + index — the index distinguishes duplicates since the correlationId is shared by the whole stream. Each cache entry is retained for the duration of its message's expiry interval (see [message level timeout](#message-level-timeout)), even beyond the end of the stream: clearing it when the stream finishes would let a late re-delivery still within its expiry window be treated as new, which is unsafe for non-idempotent commands.
- **Acknowledgement.** By default a consumer acknowledges each message as soon as it is delivered to the user. Users may opt into manual acknowledgement to finish processing a message before forgoing broker re-delivery on an unexpected crash. 
- **`isLast` receipt.** On an `isLast` control message (`c:…:last`), the consumer notifies the user that the stream has ended. This standalone message carries no payload or application-provided user properties and is **not** surfaced as a stream entry ([why `isLast` is its own message](#islast-message-being-its-own-message)). Because delivery order is guaranteed, receiving further data for that half after its `isLast` is a protocol violation.

**Producing a stream:** every data message carries the same correlation data, the appropriate [`__stream` metadata](#streaming-user-property), the serialized user payload, and any per-message metadata plus the stream's per-stream metadata (repeated on every message so it survives first-message loss), at QoS 1. The producer ends its stream with a standalone `isLast` message (no payload, no application user properties) on the same topic and correlation. Which topic each side uses, and the `$partition` requirement on the command topic, are covered below and in [exchange routing and lifetime](#exchange-routing-and-lifetime).

#### Invoker side

The invoker first subscribes to its response topic (`clients/{mqtt client id of invoker}/...`, prefixed like vanilla RPC), then publishes the request stream. In addition to the common producer fields, each request-stream message (data and the closing `isLast`) carries:

- the response topic, so the executor knows where to reply;
- the `$partition` user property set to the invoker's client id, so the shared subscription routes every message of the exchange to the same executor.

#### Executor side

The executor subscribes to the command topic using a **shared subscription** (so that, with multiple executors, only one handles each exchange), with the same topic pre/suffixing and custom-topic-token support as vanilla RPC. On the first request message it notifies the application, which then supplies the response stream; each response message is published to the response topic named in the request, with the same correlation data.

Two executor-only rules:

- If an `isLast` arrives before any data message in the request stream, log an error, acknowledge it, and ignore it — a request stream must have at least one entry. A `heartbeat` that arrives before the first request likewise establishes no exchange and is simply acknowledged and ignored.
- Unlike vanilla RPC, the executor keeps **no replay cache**: streams may grow without bound, so replaying a response stream isn't feasible.

### Timeout support

Timeout support avoids either side getting stuck — waiting for a final message that was lost or never sent, or for a peer that has silently stalled (the invoker waiting on responses, or the executor waiting on requests).

#### Decision

The invoker configures an **idle (inactivity) timeout** for the exchange and a per-message expiry for request/response data. If the user does not specify an idle timeout, the SDK defaults to 10 seconds; a user-supplied value must be positive and finite and is rounded up to whole seconds. Every exchange therefore has a positive, finite effective idle timeout of at least one second.

The idle timeout measures time **since the last message received from the peer**, not total duration. It is the backstop for an exchange that stalls before reaching [graceful completion](#core-abstractions) — a half-stream that never closes, a lost final message, or a crashed peer. The SDK sends [heartbeats](#stream-level-timeout) at a regular interval, and those steady heartbeats are what keep the peer's timer alive; other messages reset it too, but only as a side effect. An exchange therefore stays open as long as the peer is alive. The SDK produces and consumes heartbeats, so the user never constructs or sees one.

##### Stream level timeout

Every **request-direction** message (invoker → executor) carries the effective idle timeout in the `timeout_length` field of `__stream` (in seconds), repeated on each so the timeout survives loss of earlier messages and lets a different executor recover the exchange mid-stream; response-direction messages (executor → invoker) omit it. Seconds align with the MQTT message expiry interval used for other timeouts, keep the header small for long-running streams, and avoid implying a sub-second precision that isn't meaningful.

Each side runs its own idle countdown and **resets it only on a valid PUBLISH received from the peer** — data, a `heartbeat`, an `isLast`, a cancellation.

- The **invoker** starts its timer when it sends its first request. If it elapses before [graceful completion](#core-abstractions), it reports the timeout to the user and stops sending.
- The **executor** starts its timer on the first request it receives. If it elapses before [graceful completion](#core-abstractions), it reports the timeout to the user callback.

**Heartbeats.** Each side sends a standalone `heartbeat` at a regular **heartbeat interval** - half the effective idle timeout, so two per idle window - on that side's data topic and correlation. Heartbeats are the steady liveness signal. A heartbeat has no payload or application user properties; the receiver resets its idle timer, acknowledges it, and does not surface or cache it. A side begins heart-beating when its exchange becomes active — the invoker on sending its first request, the executor on receiving the first request. The side stops heart-beating once its exchange is gracefully complete or terminal.

A local idle timeout terminates only that side's exchange state; neither side sends a timeout status to the other. The peer reaches its own timeout independently if no further progress occurs.

Messages received by either side after it has timed out are acknowledged but otherwise ignored. Each party therefore keeps a per-correlation tombstone for timed-out streams so post-timeout packets aren't treated as a new stream; it is retained at least as long as the longest expiry of any packet that could still arrive.

##### Message level timeout

Users may set the message expiry interval of each **data** message in a request/response stream; by default it equals the effective idle timeout. Every stream data message _must_ include a positive, finite message expiry — a message with no (or zero) expiry is rejected, as in vanilla RPC, since an unbounded expiry would make the de-dup cache grow without bound. The receiving end uses this value as the de-dup cache length for the cached message (vanilla RPC has the [same requirement](../../reference/command-timeouts.md#input-values)).

SDK-generated control messages (`isLast`, cancellation requests, cancellation acknowledgements, and terminal errors) use the effective idle timeout as their message expiry, while the exchange is active; heartbeats instead use half the effective idle timeout length rounded up to whole seconds.

#### Alternative timeout designs considered

- The above approach, but trying to calculate time spent on broker side (using message expiry interval) so that invoker and executor timeout at the same exact time
  - This would require additional metadata in the ```__stream``` user property (intended vs received message expiry interval) and is only helpful in the uncommon scenario where a message spends extended periods of time at the broker
- Specify the number of milliseconds allowed between the executor receiving the final command request and delivering the final command response.
  - This is the approach that gRPC takes, but it doesn't account for scenarios where the invoker/executor dies unexpectedly (since gRPC relies on a direct connection between invoker and executor)
- Use the message expiry interval of the first received message in a stream to indicate the stream-level timeout
  - Misuses the message expiry interval's purpose and could lead to the broker storing messages for extended periods of time unintentionally
- Send a terminal timeout status when a local idle timer expires
  - Both sides already have local timers and terminate independently. A post-timeout status would require a separate delivery budget and could arrive after the peer is already terminal.
- Start the idle timer on the first stream message and never reset it (a single overall deadline)
  - Caps the total duration of an exchange, so it cannot support indefinitely long streams such as a continuous sensor feed.

### Cancellation support

To avoid scenarios where long-running streaming requests/responses are no longer wanted, either side may cancel a streaming RPC at any time while the exchange is active.

Since a cancellation request may expire on the broker, the sender may retransmit it while its local exchange remains active. Receiving the `Canceled` status confirms cancellation. Any other terminal outcome, including local timeout, ends retransmission without confirming cancellation. Cancellation requests may include user properties explaining why cancellation was requested.

#### API

Cancellation is exposed through the *exchange context* — returned to the invoker and passed into the executor's receive callback — rather than the per-stream *stream context*. This keeps cancellation exchange-scoped (a single cancel/timeout per invocation, not one per direction) and off the per-direction *stream context*.

Either side invokes the **cancel** operation (optionally attaching user properties) and observes peer cancellation or local timeout through the *exchange context*'s signal and its *canceled* / *timed out* flags, along with any user properties on the received cancellation. For a concrete illustration see the [appendix](#illustrative-net-api); for detailed examples see the [integration tests](../../../dotnet/test/Azure.Iot.Operations.Protocol.IntegrationTests/StreamingIntegrationTests.cs).

### Protocol layer details

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

The sender may retransmit the cancellation request while its local exchange remains active. Receiving `Canceled` confirms cancellation; any other terminal outcome ends retransmission without confirming it.

#### Receiving a cancellation

The receiver of a cancellation responds depending on the state of that receiver:

- **Still active** — notifies the application (if the RPC is still running) and replies with `Canceled` on the appropriate route (the invoker's acknowledgement travels on the command topic and carries `$partition`).
- **Already completed** (both halves closed) — acknowledges the message and sends nothing.
- **Already canceled** — re-sends `Canceled` so a retried/duplicate cancellation is answered.

### Error handling and stream termination

The **termination machinery** is symmetric across both directions; what is asymmetric is **which statuses each side originates** — inherited from RPC, where the outcome `__stat` is a response-direction concept.

Both produced streams end **gracefully** the same way: a standalone `isLast` message (no payload or application-provided user properties, a success status). Either direction can also end with the **`Canceled`** terminal that the [cancellation](#cancellation-support) mechanism produces. The directions differ only in their **error** ending:

- The **response stream** carries a `__stat` on every message and can self-terminate on error. A successful entry uses `200` when it carries a payload and `204` when it does not; neither terminates the stream. An **error status (`4xx`/`5xx`) is self-terminating**: the executor sends nothing further, so the receiver surfaces it as the terminal error and ends the response stream. An error response does **not** also need a separate `isLast` message — its status is sufficient, and the executor may be unable to send a separate `isLast` (for example, after a crash). This covers executor exceptions (`500`) and request/protocol validation errors (`4xx`).
- The **request stream** carries no outcome `__stat`, so it has no self-terminating-error form. A request-side failure — the request pump throwing, or the application abandoning the exchange — instead terminates the exchange through a best-effort **cancellation** (see [invoker side](#invoker-side)).

Whichever side originates it, a terminal status is **exchange-scoped**, not a stream entry, and is de-duplicated using exchange terminal state keyed by correlation data rather than by index. Because it is exchange-scoped, it may arrive **after** a graceful `isLast` has already closed the data half in its direction — for example an executor error raised while the request half is still open, or a `Canceled` after the request `isLast`. Such a status does not reopen the data stream; it terminates the still-active **exchange**. If the corresponding iterator is still open the status faults it; if the iterator already completed via `isLast`, the status is observed only through the exchange context's completion.

The `__apErr` (`IsApplicationError`) property classifies an error as either a framework/protocol error (`__apErr = false`: canceled, bad request, internal error) or an application-level error (`__apErr = true`) the command logic chose to return. **Either way the error status terminates the stream** — there is no per-message error status that leaves the stream running. An application that needs a per-item outcome while the stream keeps going (for example, a batch where individual items may fail) must encode that in its response payload (`TResp`), not the protocol status — a mid-stream "failed item" is just a normal response whose payload represents the failure.

### Disconnection scenario considerations

In every case, QoS 1 sessions carry queued messages across a reconnection (within each message's expiry), and whichever side stops seeing progress reaches its own local [idle timeout](#stream-level-timeout) independently.

- Invoker side disconnects unexpectedly while sending requests
  - On reconnection, the request messages queued in its session client publish as expected and the exchange resumes
  - Otherwise the executor stops seeing requests and its idle timeout fires
- Invoker side disconnects unexpectedly while receiving responses
  - The broker holds each published response for its [message-level timeout](#message-level-timeout) (message expiry interval) and redelivers those still within their expiry on reconnection; those whose expiry lapses first are lost
  - If the invoker's session is lost, the exchange cannot resume and the executor's idle timeout fires
- Executor side isn't connected when the invoker sends the first request
  - The broker may return a "no matching subscribers" PUBACK; whether to retry here is TBD
  - On a success PUBACK the request is held for its message expiry, and the invoker's idle timeout fires if no executor consumes it in time
- Executor side disconnects unexpectedly while receiving requests
  - The broker holds each published request for its [message-level timeout](#message-level-timeout) (message expiry interval) and redelivers those still within their expiry on reconnection; those whose expiry lapses first are lost
  - If the executor's session is lost, the invoker's idle timeout fires
- Executor side disconnects unexpectedly while sending responses
  - On reconnection, the response messages queued in its session client publish as expected and the exchange resumes
  - Otherwise the invoker stops seeing responses and its idle timeout fires

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

The following C# sketches one possible implementation of the [API design](#api-design) above. It is illustrative only — the SDKs also target Rust and Go, which will expose equivalent shapes idiomatically.

Two base classes define the pattern — `StreamingCommandInvoker` and `StreamingCommandExecutor` — reusing "extended" request/response types that pair each payload with its per-message metadata:

```csharp
public class StreamingExtendedRequest<TReq>
    where TReq : class
{
    public TReq Payload { get; set; }
    public StreamMessageMetadata Metadata { get; set; }
    // Per-message MQTT expiry; defaults to the idle timeout and must be <= it.
    public TimeSpan? MessageExpiry { get; set; }
}

public class StreamingExtendedResponse<TResp>
    where TResp : class
{
    public TResp Payload { get; set; }
    public StreamMessageMetadata Metadata { get; set; }
    public TimeSpan? MessageExpiry { get; set; }
}

// SDK-assigned index, HLC timestamp, and per-message user properties.
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

// Per-stream metadata is asymmetric, mirroring vanilla RPC's request/response metadata.
public class RequestStreamMetadata
{
    ...
}

public class ResponseStreamMetadata
{
    ...
}
```

The **stream context** wraps a stream's entries; its per-stream metadata travels separately as a `RequestStreamMetadata` / `ResponseStreamMetadata`. The **exchange context** carries per-exchange completion, cancellation, and timeout:

```csharp
public interface IStreamContext<T>
    where T : class
{
    IAsyncEnumerable<T> Entries { get; set; }
}

// The invoker returns before the first response arrives, so await StreamMetadata for the response
// stream's metadata (it faults if the exchange ends before any response).
public interface IResponseStreamContext<T> : IStreamContext<T>
    where T : class
{
    Task<ResponseStreamMetadata> StreamMetadata { get; }
}

public interface IExchangeContext
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

The invoker supplies the request stream (and its stream-level metadata) and returns the response stream plus the exchange context:

```csharp
public abstract class StreamingCommandInvoker<TReq, TResp>
    where TReq : class
    where TResp : class
{
    // false -> the caller must ack each response entry via ReceivedStreamingExtendedResponse.AcknowledgeAsync.
    public bool AutomaticallyAcknowledgeResponses { get; set; } = true;

    // Returns after the first request is accepted, without waiting for the rest.
    // idleTimeout: inactivity window (default 10s), reset on each message received from the executor.
    public async Task<(IResponseStreamContext<ReceivedStreamingExtendedResponse<TResp>> Responses, IExchangeContext Exchange)> InvokeStreamingCommandAsync(
      IAsyncEnumerable<StreamingExtendedRequest<TReq>> requests,
      RequestStreamMetadata? streamMetadata = null,
      Dictionary<string, string>? additionalTopicTokenMap = null,
      TimeSpan? idleTimeout = default,
      CancellationToken cancellationToken = default) {...}
}
```

The executor's callback receives the request stream, its stream-level metadata, and the exchange context, and returns the response stream together with its metadata:

```csharp
public abstract class StreamingCommandExecutor<TReq, TResp> : IAsyncDisposable
    where TReq : class
    where TResp : class
{
    public required Func<
        IStreamContext<ReceivedStreamingExtendedRequest<TReq>>,
        RequestStreamMetadata,
        IExchangeContext,
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
