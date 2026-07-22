# RPC Streaming Lifecycle Diagrams

> Supplementary, non-authoritative visual reference for [ADR 25: RPC Streaming](0025-rpc-streaming.md).
> The ADR is the source of truth.

## 1. Shared Lifecycle

Both roles run the **same** local state machine. Each side **produces** one stream and **consumes** the
other, and the transition labels are written from that side's own view: *my* `isLast` closes the stream
I produce, and the *peer's* `isLast` closes the stream I consume. The exchange is **gracefully complete**
only once *both* halves are closed.

| Role | Produces — closed by *my* `isLast` | Consumes — closed by *peer's* `isLast` |
| --- | --- | --- |
| Invoker | request stream | response stream |
| Executor | response stream | request stream |

```mermaid
stateDiagram-v2
    [*] --> Active: exchange established

    state Active {
        [*] --> BothOpen
        BothOpen --> ProducedClosed: send my isLast
        BothOpen --> ConsumedClosed: receive peer's isLast
        ProducedClosed --> BothClosed: receive peer's isLast
        ConsumedClosed --> BothClosed: send my isLast
    }

    BothClosed --> Completed
    Active --> Canceled: peer cancel or confirmed local cancel
    Active --> TimedOut: local idle timeout
    Active --> Failed: local failure or terminal error

    Completed --> [*]
    Canceled --> [*]
    TimedOut --> [*]
    Failed --> [*]
```

A non-success terminal — `Canceled`, `TimedOut`, or `Failed` — ends the whole exchange from any active
state, regardless of which halves are still open. Establishment is role-specific (the invoker sends
`request[0]`; the executor receives it); see §2.

## 2. Invoker Establishment and Full Duplex

The invocation returns after the mandatory first request is sent. It does not wait for a
second request or request-stream completion. A fast response can arrive through the broker
before the return and is retained until the application begins iteration.

```mermaid
sequenceDiagram
    autonumber
    participant IA as Invoker app
    participant IS as Invoker SDK
    participant B as MQTT broker
    participant ES as Executor SDK
    participant EA as Executor app

    IA->>IS: InvokeStreamingCommand, request stream
    IS->>B: Subscribe response topic
    IS->>B: PUBLISH request[0]<br/>command topic, $partition=P
    B->>ES: request[0]
    ES->>EA: OnStreamingCommandReceived, first request
    EA-->>ES: response[0]
    ES->>B: PUBLISH response[0]<br/>response topic
    B->>IS: response[0]
    Note over IS: Buffer response[0]<br/>call has not returned yet
    IS-->>IA: Return response stream and exchange context
    Note over IA,IS: Return follows request[0] publication,<br/>it does not wait for more requests
    IA->>IS: Iterate response stream
    IS-->>IA: Deliver buffered response[0]
    Note over IA: response[0] enables request[1]
    IA->>IS: Yield request[1]
    IS->>B: PUBLISH request[1]<br/>command topic, $partition=P
    B->>ES: request[1]
    ES->>EA: Deliver request[1]
```

## 3. Normal Bidirectional Exchange

A fuller happy path across both apps and SDKs. Beyond the interleaved data flow it shows per-entry
**indexes**, the `__stream` header, response **`__stat`** (`200` with a payload, `204` without), a
regular **heartbeat** filling a quiet gap, de-dup and idle-timer reset on receipt, standalone `isLast`,
and independent half-close. Requests and responses may interleave, either data half may close first, and
both control lanes stay active until the exchange is terminal.

```mermaid
sequenceDiagram
    autonumber
    participant IA as Invoker app
    participant IS as Invoker SDK
    participant B as MQTT broker
    participant ES as Executor SDK
    participant EA as Executor app

    Note over IA,EA: One correlation GUID for the whole exchange<br/>invoker produces requests, executor produces responses

    IA->>IS: Yield request[0]
    IS->>B: PUBLISH request[0]<br/>__stream=0:false:false:false:T, expiry=T, $partition=P
    B->>ES: request[0]
    Note over ES: De-dup by correlationId+index<br/>reset idle timer
    ES->>EA: Deliver request[0] (index 0)

    EA-->>ES: Yield response[0] (with payload)
    ES->>B: PUBLISH response[0]<br/>__stream=0:false:false:false, __stat=200
    B->>IS: response[0]
    Note over IS: Reset idle timer<br/>deliver in index order
    IS-->>IA: Deliver response[0] (index 0)

    IA->>IS: Yield request[1]
    IS->>B: PUBLISH request[1]<br/>__stream=1:false:false:false:T
    B->>ES: request[1]
    ES->>EA: Deliver request[1] (index 1)

    Note over ES: Heartbeat interval elapsed<br/>emit heartbeat
    ES->>B: heartbeat<br/>__stream=0:false:false:true, response topic
    B->>IS: heartbeat
    Note over IS: Inbound PUBLISH resets idle timer<br/>not surfaced to the app

    EA-->>ES: Yield response[1] (no payload)
    ES->>B: PUBLISH response[1]<br/>__stream=1:false:false:false, __stat=204
    B->>IS: response[1]
    IS-->>IA: Deliver response[1] (index 1)

    Note over IA,EA: Either data half may close first, they are independent

    IA->>IS: End request stream
    IS->>B: `isLast` request<br/>__stream=2:true:false:false:T, no payload, $partition=P
    B->>ES: `isLast` request
    ES->>EA: Signal request stream ended
    Note over IS,ES: Request data half closed<br/>control lanes stay active

    EA-->>ES: End response stream
    ES->>B: `isLast` response<br/>__stream=2:true:false:false, no payload
    B->>IS: `isLast` response
    IS-->>IA: Signal response stream ended
    Note over IS,ES: Response data half closed

    Note over IA,EA: Both halves closed, exchange Completed<br/>tombstone retained for late or duplicate packets
```

## 4. Exchange Timeout

The stream timeout is an **idle (inactivity)** timeout. The invoker starts its timer when it sends its
first request; the executor starts when it receives the first request. After that, each side resets its
timer only on a valid PUBLISH **received from the peer** — a heartbeat, data, an `isLast`, or a
cancellation. A side's own sends, and the PUBACKs for them, do not reset it; duplicate, malformed, and
late packets do not count either.

Because each side emits [heartbeats](0025-rpc-streaming.md#stream-level-timeout) at a regular interval
(half of `T`, so about two per window), a live peer keeps resetting the timer even when it has no data
to send. A side moves to `TimedOut` only after `T` elapses with no inbound PUBLISH from the peer — that
is, once the peer stops both its data and its heartbeats (a crash, a disconnect, or completion with a
lost final message). Timeout is purely local: the SDK reports it to its own application and sends no
timeout status, so the peer reaches its own timeout independently. The sequence below shows the executor
going silent; the invoker then times out. The symmetric case — the invoker going silent and the
executor timing out — works identically.

```mermaid
sequenceDiagram
    autonumber
    participant IA as Invoker app
    participant IS as Invoker SDK
    participant B as MQTT broker
    participant ES as Executor SDK
    participant EA as Executor app

    IS->>B: PUBLISH request[0]<br/>timeout=T, $partition=P
    Note over IS: Sent first request<br/>start idle timer T
    B->>ES: request[0]
    Note over ES: First request received<br/>start idle timer T
    ES->>EA: Deliver request[0]
    EA-->>ES: response[0]
    ES->>B: response[0], response topic
    B->>IS: response[0]
    Note over IS: Inbound PUBLISH from peer<br/>reset idle timer T
    IS-->>IA: Deliver response[0]
    IS--xB: PUBACK for response[0] lost

    Note over ES: Heartbeat interval elapsed<br/>emit heartbeat
    ES->>B: heartbeat, __stream=0:false:false:true<br/>response topic
    B->>IS: heartbeat
    Note over IS: Inbound PUBLISH from peer<br/>reset idle timer T
    Note over IS: Heartbeat interval elapsed<br/>emit heartbeat
    IS->>B: heartbeat, __stream=0:false:false:true:T<br/>command topic, $partition=P
    B->>ES: heartbeat
    Note over ES: Inbound PUBLISH from peer<br/>reset idle timer T

    Note over ES,EA: Executor disconnects or crashes<br/>stops sending responses and heartbeats
    Note over IS: No inbound PUBLISH from peer for T
    Note over IS: Local exchange enters TimedOut<br/>stop request production
    IS-->>IA: Report timeout
    Note over IS,B: Invoker sends no timeout PUBLISH

    B->>IS: response[0] redelivered (DUP)<br/>QoS 1, because the earlier PUBACK was lost
    IS-->>B: PUBACK response[0]
    Note over IS: Invoker already TimedOut<br/>tombstone acknowledges and ignores the duplicate
```

Because no timeout status is ever sent, each side simply retains a tombstone for as long as any
in-flight data packet could still arrive. In the example, the invoker's PUBACK for `response[0]` is
lost, so the broker redelivers it (QoS 1, `DUP` set); arriving after the invoker has timed out, it is
matched to the tombstone, acknowledged, and ignored.

## 5. Invoker-Initiated Cancellation

The cancellation request travels on the **command topic** and retains `$partition`. The `Canceled`
status travels on the **response topic**. A lost status can be recovered by retrying the cancellation
request and re-answering from terminal tombstone state.

```mermaid
sequenceDiagram
    autonumber
    participant IA as Invoker app
    participant IS as Invoker SDK
    participant B as MQTT broker
    participant ES as Executor SDK
    participant EA as Executor app

    IS->>B: request[0], command topic, $partition=P
    B->>ES: request[0]
    ES->>EA: Deliver request[0]
    IA->>IS: Cancel
    IS->>B: cancel, __stream=0:true:true:false:T, $partition=P
    B->>ES: cancel request
    ES->>EA: Stop callback
    Note over ES: Local exchange becomes Canceled
    ES->>B: Canceled, __stream=0:false:false:false, response topic

    alt Canceled is delivered
        B->>IS: Canceled
    else Canceled expires at the broker
        Note over B: Canceled dropped before delivery
        IS->>B: retry cancel, same correlation, $partition=P
        B->>ES: duplicate cancel
        Note over ES: Canceled tombstone re-answers
        ES->>B: resend Canceled, __stream=0:false:false:false
        B->>IS: Canceled
    end

    IS-->>IA: Exchange Canceled, cancel completes
    Note over IS,ES: Late data is acknowledged and ignored
```

## 6. Executor-Initiated Cancellation

This example starts after the executor has closed its response data half. The **response topic** still
carries the cancellation request, and the **command topic** still carries the invoker's `Canceled`
acknowledgement.

```mermaid
sequenceDiagram
    autonumber
    participant IA as Invoker app
    participant IS as Invoker SDK
    participant B as MQTT broker
    participant ES as Executor SDK
    participant EA as Executor app

    IS->>B: request[0], command topic, $partition=P
    B->>ES: request[0]
    ES->>B: `isLast` response
    B->>IS: `isLast` response
    Note over IS,ES: Response data half closed<br/>both control lanes remain active
    EA->>ES: Cancel
    ES->>B: cancel, __stream=0:true:true:false, response topic
    B->>IS: cancel request
    IS->>IA: Signal cancellation
    Note over IS: Stop request production<br/>local exchange becomes Canceled
    IS->>B: Canceled, __stream=0:false:false:false:T, command topic, $partition=P

    alt Canceled is delivered
        B->>ES: Canceled
    else Canceled expires at the broker
        Note over B: Canceled dropped before delivery
        ES->>B: retry cancel, same correlation
        B->>IS: duplicate cancel
        Note over IS: Canceled tombstone re-answers
        IS->>B: resend Canceled, $partition=P
        B->>ES: Canceled
    end

    ES-->>EA: Exchange Canceled, cancel completes
```

## 7. Executor Error Status

A response `__stat` error code (`4xx`/`5xx`) is **self-terminating**: the executor sends nothing
further — not even an `isLast` — and the receiver surfaces it as the terminal error. Because the status
is **exchange-scoped**, it ends the whole exchange, tearing down an open request half as well. `__apErr`
distinguishes an application error the command returned (`true`) from a framework or protocol error
(`false`). This is a **response-direction** terminal; the request direction has no equivalent (see §8).

```mermaid
sequenceDiagram
    autonumber
    participant IA as Invoker app
    participant IS as Invoker SDK
    participant B as MQTT broker
    participant ES as Executor SDK
    participant EA as Executor app

    IS->>B: request[0], command topic, $partition=P
    B->>ES: request[0]
    ES->>EA: Deliver request[0]
    EA-->>ES: response[0]
    ES->>B: response[0], response topic
    B->>IS: response[0]
    IS-->>IA: Deliver response[0]

    Note over IS,ES: Request data half still open<br/>isLast request not yet sent
    EA->>ES: Fail with an error, 4xx or 5xx
    Note over ES: Error status is self-terminating, no isLast response is sent<br/>__apErr flags application vs framework error
    ES->>B: error 5xx, __stream=0:false:false:false, response topic
    Note over ES: Local exchange enters Failed
    B->>IS: error 5xx status
    IS-->>IA: Fault response iterator, surface the error
    Note over IS: Local exchange enters Failed<br/>stop request production
    Note over IS,ES: The error terminates the whole exchange<br/>the open request half is torn down without an isLast
    Note over IS,ES: Later data is acknowledged and ignored via tombstone
```

If the invoker's response iterator has already completed via `isLast`, a later error is observed only
through the exchange context's completion rather than faulting the already-finished iterator.

## 8. Request-Side Failure and Cancellation

The request direction carries no outcome `__stat`, so a request-side failure — the request pump
throwing, or the application abandoning the exchange — cannot self-terminate with an error. Instead the
invoker faults its local exchange with the error, stops publishing, and terminates the peer through a
best-effort **cancellation**; the only terminal status the request direction ever carries is `Canceled`.

```mermaid
sequenceDiagram
    autonumber
    participant IA as Invoker app
    participant IS as Invoker SDK
    participant B as MQTT broker
    participant ES as Executor SDK
    participant EA as Executor app

    IS->>B: request[0], command topic, $partition=P
    B->>ES: request[0]
    ES->>EA: Deliver request[0]
    Note over IA,IS: Producing request[1] throws in the request pump
    Note over IS: Stop request publication<br/>local exchange faults with the error
    Note over IS: The request direction has no error status<br/>terminate the peer via best-effort cancellation
    IS->>B: cancel, __stream=0:true:true:false:T, $partition=P
    B->>ES: cancel request
    ES->>EA: Stop callback
    Note over ES: Local exchange becomes Canceled
    ES->>B: Canceled, __stream=0:false:false:false, response topic
    B->>IS: Canceled
    IS-->>IA: Exchange faulted with the request-pump error
    Note over IS,ES: Cancellation is best-effort<br/>the invoker faulted locally regardless of the ack
```

The cancellation is best-effort: the invoker has already faulted locally and surfaces the original
error regardless of whether the `Canceled` acknowledgement arrives.

## 9. Incoming Packet Classification and Terminal Races

This classifier assumes correlation lookup has found an active exchange or a retained
terminal tombstone. Initial request validation is outside this diagram. A timeout is never
received as a packet — it is a local idle event (see §4) — so it does not appear here.

```mermaid
flowchart TD
    P["Incoming MQTT PUBLISH"] --> S{"__stream present?"}
    S -- "No" --> O["Route to another protocol handler"]
    S -- "Yes" --> T{"Exchange already terminal?"}
    T -- "Yes" --> RC{"Retried cancel and<br/>state is Canceled?"}
    RC -- "Yes" --> RA["Re-send Canceled"]
    RC -- "No" --> AI["Acknowledge and ignore"]
    T -- "No" --> C{"cancelRequest = true?"}
    C -- "Yes" --> PC["Notify application<br/>send Canceled, enter Canceled"]
    C -- "No" --> E{"Terminal error status?"}
    E -- "Canceled" --> CAN["Enter Canceled"]
    E -- "Other 4xx or 5xx" --> F["Enter Failed"]
    E -- "No" --> HB{"heartbeat = true?"}
    HB -- "Yes" --> HBR["Reset idle timer<br/>acknowledge, do not deliver or cache"]
    HB -- "No" --> L{"isLast = true?"}
    L -- "Yes" --> HC["Close this data half"]
    HC --> BC{"Both data halves closed?"}
    BC -- "Yes" --> CO["Enter Completed"]
    BC -- "No" --> AC["Remain active<br/>keep control lanes open"]
    L -- "No" --> D["Deduplicate by correlation and index<br/>deliver data entry"]
    PC --> TS["Retain terminal tombstone"]
    CAN --> TS
    F --> TS
    CO --> TS
```

## Coverage

| Diagram | ADR concern |
| --- | --- |
| Shared lifecycle | Core abstractions, graceful completion, terminal states |
| Invoker establishment | Full-duplex return semantics and early-response buffering |
| Normal exchange | Interleaving, independent half-close, control-lane lifetime |
| Timeout | Idle timers reset on PUBLISHes received from the peer, heartbeats keep them alive, both sides terminate locally with no wire status, tombstones |
| Invoker cancellation | Command-topic affinity, retries, `Canceled` response |
| Executor cancellation | Control after half-close, request-direction `Canceled` |
| Executor error | Self-terminating response `__stat`, whole-exchange teardown, `__apErr` |
| Request-side failure | Request direction has no error status, best-effort cancellation |
| Packet classification | `__stream` routing, terminal precedence, late packets |
