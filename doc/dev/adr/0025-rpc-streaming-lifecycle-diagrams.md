# RPC Streaming Lifecycle Diagrams

> Supplementary, non-authoritative visual reference for [ADR 25: RPC Streaming](0025-rpc-streaming.md).
> The ADR is the source of truth.

## 1. Shared Lifecycle

Read this as one local state machine per role. Sending and receiving in transition labels
mean the invoker and executor views, respectively.

```mermaid
stateDiagram-v2
    [*] --> InvokerEstablishing
    [*] --> ExecutorWaiting
    InvokerEstablishing --> Active: response reception active, request 0 sent, contexts returned
    ExecutorWaiting --> Active: request 0 received

    state Active {
        [*] --> BothOpen
        BothOpen --> RequestClosed: isLast request sent or received
        BothOpen --> ResponseClosed: isLast response sent or received
        RequestClosed --> BothClosed: isLast response sent or received
        ResponseClosed --> BothClosed: isLast request sent or received
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

| Data half closes | Invoker event | Executor event |
| --- | --- | --- |
| Request | Sends `isLast` request | Receives `isLast` request |
| Response | Receives `isLast` response | Sends `isLast` response |

`Completed` requires both data halves to close. Any non-success terminal transition ends
the whole exchange from any active half-close state.

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

Requests and responses may interleave. Either data half may close first, but both control
lanes continue carrying exchange controls until the exchange is terminal.

```mermaid
sequenceDiagram
    autonumber
    participant IS as Invoker SDK
    participant B as MQTT broker
    participant ES as Executor SDK

    Note over IS,ES: Invoker app produces requests and consumes responses,<br/>executor app the reverse

    IS->>B: request[0], command topic, $partition=P
    B->>ES: request[0]
    ES->>B: response[0], response topic
    B->>IS: response[0]
    IS->>B: request[1], command topic, $partition=P
    B->>ES: request[1]
    ES->>B: response[1], response topic
    B->>IS: response[1]

    alt Request data half closes first
        IS->>B: `isLast` request, $partition=P
        B->>ES: `isLast` request
        Note over IS,ES: Request data half closed<br/>both control lanes remain active
        ES->>B: `isLast` response
        B->>IS: `isLast` response
    else Response data half closes first
        ES->>B: `isLast` response
        B->>IS: `isLast` response
        Note over IS,ES: Response data half closed<br/>both control lanes remain active
        IS->>B: `isLast` request, $partition=P
        B->>ES: `isLast` request
    end

    Note over IS,ES: Both data halves closed<br/>exchange Completed, tombstone retained
```

## 4. Exchange Timeout

The stream timeout is an **idle (inactivity)** timeout. The invoker starts its timer on the first
PUBACK for a request PUBLISH, while the executor starts on the first new, valid request PUBLISH it
receives. After that, each side resets its timer when it receives either a new, valid stream PUBLISH
or the first PUBACK for one of its own stream PUBLISH packets. Duplicate, malformed, and late packets
do not count as progress.

A side moves to `TimedOut` only after `T` elapses with no progress. Timeout is purely local: the SDK
reports it to its own application and sends no timeout status, so the peer reaches its own timeout
independently. The sequence below expands every timer-relevant event. It illustrates a broker ordering
in which the executor receives the PUBACK for its final response before the invoker receives that
response, so the executor's last reset occurs first. The roles reverse if the invoker has the earlier
last reset.

```mermaid
sequenceDiagram
    autonumber
    participant IA as Invoker app
    participant IS as Invoker SDK
    participant B as MQTT broker
    participant ES as Executor SDK
    participant EA as Executor app

    IA->>IS: Yield request[0]
    IS->>B: PUBLISH request[0]<br/>timeout=T, $partition=P
    B-->>IS: PUBACK for request[0]
    Note over IS: First PUBACK for own PUBLISH<br/>start idle timer T

    B->>ES: PUBLISH request[0]
    Note over ES: First new, valid inbound PUBLISH<br/>start idle timer T
    ES-->>B: PUBACK for request[0]
    ES->>EA: Deliver request[0]

    EA-->>ES: Yield response[0]
    ES->>B: PUBLISH response[0]
    B-->>ES: PUBACK for response[0]
    Note over ES: First PUBACK for own PUBLISH<br/>reset idle timer T

    B->>IS: PUBLISH response[0]
    Note over IS: New, valid inbound PUBLISH<br/>reset idle timer T
    IS--xB: PUBACK for response[0] is lost
    IS-->>IA: Deliver response[0]

    EA-->>ES: End response stream
    ES->>B: PUBLISH `isLast` response
    B-->>ES: PUBACK for `isLast` response
    Note over ES: First PUBACK for own PUBLISH<br/>reset idle timer T

    B->>IS: PUBLISH `isLast` response
    Note over IS: New, valid inbound PUBLISH<br/>reset idle timer T
    IS-->>B: PUBACK for `isLast` response
    IS-->>IA: Complete response stream
    Note over IS,ES: Response data half closed<br/>`isLast` request is lost or withheld

    Note over ES: No new valid PUBLISH or first PUBACK for T
    Note over ES: Local exchange enters TimedOut
    ES-->>EA: Report timeout and ask callback to stop
    Note over ES,B: Executor sends no timeout PUBLISH

    Note over IS: No new valid PUBLISH or first PUBACK for T
    Note over IS: Local exchange enters TimedOut<br/>stop request production
    IS-->>IA: Report timeout
    Note over IS,B: Invoker sends no timeout PUBLISH

    B->>IS: PUBLISH duplicate response[0], QoS 1 redelivery
    IS-->>B: PUBACK duplicate response[0]
    Note over IS: Tombstone identifies terminal exchange<br/>acknowledge and ignore, do not reset timer
```

Because no timeout status is ever sent, each side simply retains a tombstone for as long as any
in-flight data packet could still arrive, acknowledging and ignoring late or duplicate messages.

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
    IS->>B: cancel, __stream=0:true:true:T, $partition=P
    B->>ES: cancel request
    ES->>EA: Stop callback
    Note over ES: Local exchange becomes Canceled
    ES->>B: Canceled, __stream=0:false:false, response topic

    alt Canceled is delivered
        B->>IS: Canceled
    else Canceled expires at the broker
        Note over B: Canceled dropped before delivery
        IS->>B: retry cancel, same correlation, $partition=P
        B->>ES: duplicate cancel
        Note over ES: Canceled tombstone re-answers
        ES->>B: resend Canceled, __stream=0:false:false
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
    ES->>B: cancel, __stream=0:true:true, response topic
    B->>IS: cancel request
    IS->>IA: Signal cancellation
    Note over IS: Stop request production<br/>local exchange becomes Canceled
    IS->>B: Canceled, __stream=0:false:false:T, command topic, $partition=P

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
    ES->>B: error 5xx, __stream=0:false:false, response topic
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
    IS->>B: cancel, __stream=0:true:true:T, $partition=P
    B->>ES: cancel request
    ES->>EA: Stop callback
    Note over ES: Local exchange becomes Canceled
    ES->>B: Canceled, __stream=0:false:false, response topic
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
    E -- "No" --> L{"isLast = true?"}
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
| Timeout | Idle timers reset on progress, both sides terminate locally with no wire status, tombstones |
| Invoker cancellation | Command-topic affinity, retries, `Canceled` response |
| Executor cancellation | Control after half-close, request-direction `Canceled` |
| Executor error | Self-terminating response `__stat`, whole-exchange teardown, `__apErr` |
| Request-side failure | Request direction has no error status, best-effort cancellation |
| Packet classification | `__stream` routing, terminal precedence, late packets |
