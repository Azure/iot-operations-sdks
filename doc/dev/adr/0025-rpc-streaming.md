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
 - If an executor crashes for any reason, the request stream MUST be able to be picked up by another executor in the group — or by the same executor after it restarts.

## Non-requirements

 - Different payload shapes per command response/request
 - Chunking a single large payload across multiple stream messages
   - Any stream message may be lost (message expiry or other circumstances), so a chunked payload could not be reliably reassembled.
 - Delivering streamed requests/responses in their **intended** order.
   - Any stream message may be lost, so entries are surfaced in the order received; reconstructing the intended order is left to the application.
 - Resuming an exchange after the **invoker** crashes or loses its session.
   - The invoker's response topic is unique to it (no load-balancing), so in-flight responses cannot be re-routed.

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

Each producer ends its own stream with a **`last`** signal. An exchange is **gracefully complete** only when *both* of its streams have closed: the invoker has sent its `last` request **and** received the `last` response, and symmetrically the executor has received the `last` request **and** sent the `last` response. Closing one stream (via `last`) does **not** end the exchange — a side that finishes its own stream early stays active until the other stream closes too, or until the [exchange timeout](#exchange-level-timeout) fires. Any other terminal — cancellation or timeout — ends the whole exchange immediately. Requiring both streams to close is the shared definition of completion used by [timeout](#timeout-support) and [cancellation](#cancellation-support).

#### Invoker behavior

The invoker supplies the outbound **request stream** together with that stream's metadata; it must contain at least one entry. The invocation establishes the exchange; it does **not** represent completion of the request stream. The invoker activates response reception, sends the mandatory first request, and then returns the inbound **response stream** — without waiting for the second request or for the request stream to end.

An empty request stream or setup error fails the invocation before an exchange is returned. Any later request-sending error terminates the local exchange, stops request publication, and triggers a best-effort cancellation; it is surfaced through the exchange's completion signal and, while still open, the response stream.

#### Executor behavior

The streaming command executor receives the inbound **request stream** and that request stream's metadata, returns the outbound **response stream** together with that stream's metadata, and can cancel or observe the exchange's completion and timeout.

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

| Field | Type | Meaning |
| ---- | ---- | ---- |
| `message_index` | uint | **data**/**control**: position of this message within the producer's stream (data and control share one counter). **status**: the index of the **received** message the status refers to (the peer's counter, not the producer's). |
| `control_command_word` | `cancel` \| `last` | **control** form only. `last`: the standalone final message that closes the producer's stream (no payload or application-provided user properties). `cancel`: a cancellation request for the exchange. |
| `timeout_length` | uint | The invoker's current timeout counter (exchange time remaining), in seconds; **request-direction only** (invoker → executor), omitted on response-direction messages. |

Examples:

- ```d:0:10``` — a request-direction data message at stream index `0`; the invoker's remaining exchange timeout is 10 seconds.
- ```c:4:last:6``` — the request stream's final (`last`) message at index `4`; the invoker's remaining exchange timeout has dropped to 6 seconds (the field is a live countdown).
- ```c:2:cancel:10``` — a request-direction cancellation at the producer's next index (`2`).
- ```s:1:10``` — a request-direction status about the received message at stream index `1`; its `__stat` value carries the error status for that message.
- ```d:0``` / ```c:7:last``` / ```s:3``` — the response-direction counterparts (executor → invoker); identical forms except `timeout_length` is omitted.

Every MQTT PUBLISH belonging to a streaming exchange must include `__stream` in exactly one of these three forms: data messages use the `d` form, control messages use the `c` form (`c:…:cancel`, `c:…:last`), and status messages use the `s` form. A status message carries no stream entry; its `__stat` property carries the outcome details for the received message it references.

[see cancellation support](#cancellation-support) and [timeout support](#timeout-support) for how these fields are used.

#### Topics and routing

A single **correlation GUID** identifies the whole exchange; every message of both streams carries it. The exchange uses **two MQTT topics** — the **request topic** (invoker → executor) and the **response topic** (executor → invoker) — and each carries that direction's data, control, and status messages together.

The executor subscribes to the **command topic** with a **shared subscription** (so that, with multiple executors, only one handles each exchange), with the same topic pre/suffixing and custom-topic-token support as vanilla RPC. The **response topic** is `clients/{invoker client id}/...` (prefixed like vanilla RPC), unique to the invoker and not shared; the invoker subscribes to it before publishing. Each request-stream message the invoker publishes carries the response topic (so the executor knows where to reply) and a `$partition` user property set to the invoker's client id.

Because the command topic is a shared subscription, **every** command-topic packet for an exchange — request data, a `last` request, invoker cancellation, and the invoker's `Canceled` acknowledgement — must carry that same `$partition`, so the broker routes them all to the same executor; otherwise a control packet could reach a different executor that holds no state for the correlation and be silently dropped. Response-topic packets need only the correlation data, since that topic is already unique to the invoker.

#### Exchange lifetime

Because closing one stream does not end the exchange (see [exchange completion](#exchange-completion)), each endpoint keeps its per-correlation state active until the exchange is terminal, so control still flows after a `last`. Once a side is terminal, further data messages for that correlation are acknowledged and ignored; only the required control re-answers (for example, re-sending `Canceled` for a re-issued cancellation) are sent. The per-correlation state is kept as a **tombstone** so late or duplicate packets remain routable and are not treated as a new stream; it is retained at least as long as the longest expiry of any packet that could still arrive, plus some buffer.

#### Producing and consuming

A side **consumes** one stream and **produces** the other. The consuming and producing rules below are identical for both roles; two further rules apply to the **executor** only.

**Consuming a stream:**

- **De-dup caching.** A consumer de-dups received data messages (QoS 1 may re-deliver) by correlationId + index + timestamp — the index distinguishes duplicates within a stream, while the per-entry HLC timestamp separates a genuine redelivery (same index *and* timestamp) from a producer that restarted and reused an index with a new timestamp (see [disconnection and recovery](#disconnection-and-recovery)). Each cache entry is retained for the duration of its message's expiry interval (see [message level timeout](#message-level-timeout)), even beyond the end of the stream: clearing it when the stream finishes would let a late re-delivery still within its expiry window be treated as new, which is unsafe for non-idempotent commands.
- **Stream entry acknowledgement.** Acknowledging an entry is a **stream-level** signal that the consumer is done with it; the framework maps it to the underlying transport acknowledgement, which the consumer never handles directly. By default an entry is acknowledged as soon as it is delivered to the user. **Manual** acknowledgement — holding the ack until processing finishes, so an unacknowledged entry is redelivered rather than lost if the consumer crashes first — is available only to the **executor** (request consumption); the invoker cannot resume a response stream after a crash, so it always auto-acknowledges.
- **`last` receipt.** On a `last` control message (`c:…:last`), the consumer notifies the user that the stream has ended; it is **not** surfaced as a stream entry ([why `last` is its own message](#last-message-being-its-own-message)). Because delivery order is guaranteed, receiving further data for that stream after its `last` is a protocol violation.

**Producing a stream:** every data message carries the same correlation data, the appropriate [`__stream` metadata](#streaming-user-property), the serialized user payload, and any message metadata plus the stream metadata, at QoS 1. The producer ends its stream with a standalone `last` message on the same topic and correlation. Which topic each side uses, and the `$partition` requirement on the command topic, are covered in [topics and routing](#topics-and-routing) above.

**Executor-only rules:**

- A `last` in the request stream with no preceding data entry is a [protocol violation](#error-handling) (a request stream must have at least one entry): the executor returns the error status and the exchange ends.
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

##### Message level timeout

We will allow users to set the message expiry interval of each message in a request/response stream manually; if unset, it defaults to the exchange timeout's **current remaining value** at the moment the message is sent. A manually set value is always **capped** at that same current remaining exchange timeout — so a message can never outlive the exchange, and the usable expiry only shrinks as the exchange counts down. Every stream message _must_ include a positive, finite message expiry — a message with no (or zero) expiry is rejected. The receiving end uses this value as the de-dup cache length for the cached message (vanilla RPC has the [same requirement](../../reference/command-timeouts.md#input-values)).


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

Either side may cancel a streaming RPC at any time while the exchange is active — for instance when a long-running request or response stream is no longer wanted.

#### Sending a cancellation

Either side cancels by publishing a [`cancel` control message](#streaming-user-property) (`c:…:cancel`), no payload, the same correlation data, on the topic it uses to reach the other party:

- The **invoker** cancels on the command topic, then keeps listening on the response topic and delivering any in-flight responses to the application until the `Canceled` status arrives and closes the channel, or the whole exchange times out.
- The **executor** cancels on the invoker's response topic, then keeps listening on the command topic and delivering any in-flight requests to the application until the `Canceled` status arrives and closes the channel, or the whole exchange times out.

Cancellation is **idempotent**: the sender may issue `cancel` more than once while exchange is active. Receiving `Canceled` confirms cancellation.

#### Receiving a cancellation

A receiver replies with a **`Canceled`** [status message](#streaming-user-property) — no payload — carrying:

- `__stream`: `s:<cancel request's index>` (status form referencing the received `cancel`)
- `__stat`: `499` (`Canceled`), plus an optional human-readable `__stMsg`

An **invoker** sending this to acknowledge an executor-initiated cancellation publishes on the command topic (request direction), so its `__stream` takes the `s:<index>:<timeout>` form — also carrying the remaining timeout; all else is identical.

Whether a receiver replies depends on its state:

- **Still active** — notifies the application, replies with `Canceled`, stops production of the outbound stream, transitions to the canceled state.
- **Already canceled** — re-sends `Canceled` so a later (re-issued) cancellation is answered.
- **Other terminal states** — acknowledges the message and sends nothing.

### Error handling

A **protocol violation** is a message that **belongs to the exchange** (its correlationId matches) yet breaks the wire contract. Concretely, a correlation-matched message violates the protocol when it:

- carries a payload that cannot be **deserialized** to the stream's expected type;
- has a missing or **malformed `__stream`** property;
- declares an **incompatible streaming protocol version**;
- is published at **QoS 0** (every stream message must be QoS 1); or
- **breaks stream sequencing** — a data entry after the stream's `last`, or a request stream whose `last` arrives with no preceding data entry.

Any such violation is **terminal**: the recipient sends a [status message](#streaming-user-property) back to the sender (`s:<index>` references the offending message, `__stat` carries the error code, with an optional human-readable `__stMsg`), and **both parties then treat the exchange as over and send no further entries**. The referenced index is **diagnostic context only** — since the exchange ends, nothing needs to correlate it back to a specific message. Unmatched or junk data (no correlating exchange) is acknowledged and discarded, never terminating a stream.

None of these should occur between conforming implementations — they indicate data corruption or a peer that does not follow the protocol. Application-level errors are **out of scope**: the protocol does not carry them, so an application that wants to signal one sends it as an ordinary data entry. The vanilla-RPC `__apErr` (`IsApplicationError`) header is **never set on a streaming message** and is **not** part of the streaming protocol. A streaming message that nonetheless carries `__apErr` is accepted and ignored. Accepted messages carry no status; success is implicit.

### Exchange termination

An exchange terminates in exactly one of four ways:

- **Graceful** — each producer closes its own stream with `last`; the exchange completes once both streams have closed (see [exchange completion](#exchange-completion)).
- **Cancellation** — either side cancels and the `Canceled` (`499`) terminal ends the exchange (see [cancellation support](#cancellation-support)).
- **Timeout** — the [exchange timeout](#exchange-level-timeout) fires.
- **Protocol violation** — the recipient of a malformed in-exchange message returns an error status and both sides end the exchange (see [error handling](#error-handling)).

A fatal failure — a crashed peer, an unhandled exception, or a request pump that throws — surfaces as a best-effort **cancellation**, or, failing that, as the peer's timeout.

### Disconnection and recovery

Streaming inherits the [MQTT session client](../../reference/session-client.md)'s reconnection and recovery semantics — which are **more restrictive** than raw MQTT 5's — so recovery hinges on whether the disconnected side's **session** survives the reconnect. With a persistent session (clean start off, within its session-expiry interval), queued outbound PUBLISHes flush and unacknowledged inbound PUBLISHes redeliver at QoS 1 — de-duplicated by index — so the exchange resumes where it left off. If the session is lost, that side's stream state is gone, the exchange cannot resume, and the peer falls back to its [exchange timeout](#exchange-level-timeout).

An in-flight message survives at the broker only for its [message expiry](#message-level-timeout), which the broker **decrements by the time the message sat queued** — so a redelivered entry has less remaining expiry than when sent and may lapse before reconnection (that entry is then lost, which is tolerated since entries are self-contained). A message's remaining expiry therefore does **not** track the exchange-timeout countdown; the two are independent clocks.

Because the command topic is a [shared subscription](#topics-and-routing), an executor crash does not strand the exchange — another executor in the group takes over, recovering mid-stream from the correlation, the repeated stream metadata, and each request's `timeout_length`. The **invoker has no equivalent**: its response topic is unique to it, so if the invoker's session is lost, in-flight responses cannot be re-routed and are **lost with no recovery** (there is no invoker-side load-balancing). This is an accepted limitation.

A replacement executor — or the same executor restarted without context — resumes the **response** stream from index 0 rather than continuing, since the executor keeps [no replay cache](#producing-and-consuming). Every entry already carries an HLC **timestamp** alongside its index, and the consumer de-dups on **both**: a genuine QoS 1 redelivery repeats the same index *and* timestamp, whereas a restarted producer reuses an index with a **new** timestamp, so its fresh entries are not mistaken for duplicates. Exposing the timestamp and index on each entry also lets the consumer detect that the producer changed or restarted mid-stream.

If the invoker publishes a request and the broker replies `no matching subscribers` — **no executor is subscribed** — the request reaches no one. Although that reason code is an MQTT-level success, the streaming layer maps it to a distinct **`NoAvailableStreamingExecutor`** error: the invoker surfaces it to the user (the stream cannot push messages right now) and ends the exchange. This relies on a broker-delivery assumption spelled out in the [appendix](#no-matching-subscribers-broker-assumption).

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
    // Per-message expiry; defaults to the exchange timeout's remaining value and is capped at it.
    public TimeSpan? MessageExpiry { get; set; }
}

public class StreamingExtendedResponse<TResp>
    where TResp : class
{
    public TResp Payload { get; set; }
    public StreamMessageMetadata Metadata { get; set; }
    public TimeSpan? MessageExpiry { get; set; }
}

// Stream index + HLC timestamp (used together for de-dup and to detect an executor restart) and per-message user properties.
public class StreamMessageMetadata
{
    public uint Index { get; init; }
    public HybridLogicalClock? Timestamp { get; init; }
    public Dictionary<string, string> UserData { get; init; } = new();
}

// A consumed request entry adds manual stream entry acknowledgement (used when the executor's auto-ack is off).
// Only the executor exposes manual ack; the invoker always auto-acknowledges responses.
public class ReceivedStreamingExtendedRequest<TReq> : StreamingExtendedRequest<TReq>
    where TReq : class
{
    // Signals this stream entry is done (once-only); the framework maps it to the transport ack.
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
    // Returns after the first request is accepted, without waiting for the rest.
    // Faults with NoAvailableStreamingExecutor if no executor is subscribed for the first request.
    // exchangeTimeout: total budget for the whole exchange (a configurable default applies if unset).
    public async Task<(IResponseStream<StreamingExtendedResponse<TResp>> Responses, IExchangeHandle Exchange)> InvokeStreamingCommandAsync(
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

### Last message being its own message

Three approaches to marking the final message in a stream were considered, and this is why the other two approaches don't work:

- Carry the final-message marker on a message that also carries a fully-fledged stream entry (a user payload and/or user properties).
  - We must support ending a stream at an arbitrary time even when a fully-fledged message can't be sent, and this approach doesn't allow that.
- Allow the final-message marker on either a fully-fledged message or a standalone message with no user payload or application-provided user properties.
  - This doesn't let the receiving end distinguish "the stream is over" from "this is the final message in the stream" when the user provides no payload or user properties on streamed messages.

Because both either fail our requirements or are ambiguous in corner cases, the final-message marker is its own **standalone** `last` control message (`c:…:last`) with no user payload or application-provided user properties.

### No matching subscribers broker assumption

When the invoker receives a `no matching subscribers` PUBACK, the streaming layer assumes the broker has **dropped** the request and will **not** deliver it later even if a matching executor subscribes shortly afterward. At an MQTT level this reason code is only a success — the PUBLISH reached the broker — and the spec leaves whether the message is then discarded or eventually delivered unspecified, so this is an **MQ-broker-specific** assumption rather than a protocol guarantee. It is guarded by end-to-end tests so that a change in broker behavior is caught rather than silently breaking the protocol.
