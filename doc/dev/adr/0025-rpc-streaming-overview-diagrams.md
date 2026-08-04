# RPC Streaming — Visual Introduction

> A three-diagram primer for readers who have **not** read [ADR 25: RPC Streaming](0025-rpc-streaming.md).
> The ADR is the source of truth; this page is non-authoritative and deliberately simplified.
> For the detailed per-scenario traces, see [the lifecycle diagrams](0025-rpc-streaming-lifecycle-diagrams.md).

Today a command is strictly unary: one request in, one response out. RPC streaming adds a **new**
communication pattern — a *streaming command invoker* and a *streaming command executor* — where a
single invocation carries **many** requests and **many** responses, in either direction, at arbitrary
times, over MQTT 5. It has its own protocol version (`1.0`) and does not change unary RPC.

Three diagrams cover it:

1. [The abstractions](#1-abstractions) — what the pieces are and how they relate.
2. [The state machine](#2-protocol-state-machine) — how one invocation lives and dies.
3. [The workflow](#3-protocol-workflow) — what actually goes over the wire, including every ending.

## 1. Abstractions

One invocation is an **exchange**: a single correlation GUID that owns exactly **two half-streams** —
requests (invoker → executor) and responses (executor → invoker). Each side **produces** one of them
and **consumes** the other. Everything scoped to the invocation as a whole — completion, cancellation,
timeout — belongs to the exchange, never to one direction.

```mermaid
classDiagram
    direction LR

    class StreamingCommandInvoker["Streaming command invoker"] {
        produces the request stream
        consumes the response stream
        subscribes its own response topic
    }

    class StreamingCommandExecutor["Streaming command executor"] {
        consumes the request stream
        produces the response stream
        shares the command topic with its group
    }

    class Exchange {
        +correlationId GUID
        +exchangeTimeout seconds
        +cancel()
        +completion()
    }

    class HalfStream["Half-stream (one direction)"] {
        +index counter, per producer
        +close with last
    }

    class RequestStream["Request stream"] {
        invoker to executor
        one or more entries
    }

    class ResponseStream["Response stream"] {
        executor to invoker
        zero or more entries
    }

    class StreamEntry["Stream entry"] {
        +payload
        +messageExpiry
    }

    class MessageMetadata["Message metadata, per entry"] {
        +index
        +HLC timestamp
        +user properties
    }

    class StreamMetadata["Stream metadata, per direction"] {
        repeated on every message
        read once by the consumer
    }

    class DataMessage["Data message, tag d"] {
        carries one stream entry
    }

    class ControlMessage["Control message, tag c last"] {
        no payload, closes the producer stream
    }

    class StatusMessage["Status message, tag s"] {
        no payload, carries a status code
    }

    class CommandTopic["Command topic"] {
        shared subscription
        every packet carries the invoker partition
    }

    class ResponseTopic["Response topic"] {
        clients slash invoker id
        unique to one invoker
    }

    StreamingCommandInvoker "1" --> "0..*" Exchange : opens
    StreamingCommandExecutor "1" --> "0..*" Exchange : serves
    Exchange "1" *-- "2" HalfStream : one produced, one consumed per side
    HalfStream <|-- RequestStream
    HalfStream <|-- ResponseStream
    HalfStream "1" *-- "1" StreamMetadata
    HalfStream "1" *-- "0..*" StreamEntry
    StreamEntry "1" *-- "1" MessageMetadata
    StreamEntry ..> DataMessage : sent as
    HalfStream ..> ControlMessage : closed by
    Exchange ..> StatusMessage : cancelled or faulted by
    RequestStream ..> CommandTopic : published on
    ResponseStream ..> ResponseTopic : published on
```

| Term | What it means |
| --- | --- |
| **Exchange** | One invocation: one correlation GUID, two half-streams, one timeout, one cancellation. |
| **Half-stream** | One direction's ordered entries, with its own index counter, closed by its producer's `last`. |
| **Stream entry** | A user payload plus its per-entry metadata — index, HLC timestamp, user properties. |
| **Stream metadata** | Metadata for a whole direction. Request- and response-stream metadata are **different** (asymmetric). Repeated on every message so losing the first message does not lose it. |
| **`last`** | A standalone control message that closes the producer's own stream — no payload, no application user properties, so a stream can be ended at any moment. |
| **Status message** | No payload either — carries `499` (`Canceled`) for the whole exchange, or a `4xx`/`5xx` protocol error about a received message. |
| **Command topic** | Invoker → executor, a **shared** subscription so an executor crash does not strand the exchange. Every packet on it carries `$partition` = invoker client id, so the group keeps routing to the same executor. |
| **Response topic** | Executor → invoker, `clients/{invoker id}/...`, unique to that invoker and subscribed before the first publish. |

Every streaming PUBLISH carries a `__stream` user property in exactly one of three tagged forms, so a
message only ever carries the fields that apply to it:

| Form | Shape | Meaning |
| --- | --- | --- |
| Data | `d:<index>[:<timeout>]` | One stream entry at that index. |
| Control | `c:<index>:last[:<timeout>]` | The producer's stream ends here (shares the data index counter). |
| Status | `s:<index>[:<timeout>]` | An outcome, detailed in `__stat` — a protocol error about the received message at that index, or `499` to cancel the exchange (index then meaningless). |

The trailing `<timeout>` is the invoker's **remaining** exchange budget in seconds. It rides on
request-direction messages only, and is a live countdown — that is what lets a replacement executor
pick up an exchange mid-stream.

## 2. Protocol state machine

Both roles run the **same** local state machine; the labels are written from that side's own point of
view. The only asymmetry is which stream each side produces:

| Role | Produces — closed by *my* `last` | Consumes — closed by *peer's* `last` |
| --- | --- | --- |
| Invoker | request stream | response stream |
| Executor | response stream | request stream |

The one rule worth memorizing: **closing a stream is not ending the exchange.** A side that has sent
its `last` stays active — control traffic still flows — until the other stream closes too, or until a
non-graceful terminal fires.

```mermaid
stateDiagram-v2
    direction TB

    [*] --> Establishing

    Establishing --> Active: first request published
    Establishing --> [*]: empty request stream<br/>or setup failure

    state Active {
        direction LR
        [*] --> BothOpen
        BothOpen --> ProducedClosed: I send my last
        BothOpen --> ConsumedClosed: peer's last arrives
        ProducedClosed --> BothClosed: peer's last arrives
        ConsumedClosed --> BothClosed: I send my last
    }

    BothClosed --> Completed: both streams closed
    Active --> Canceled: Canceled status 499<br/>sent or received
    Active --> TimedOut: exchange budget elapsed
    Active --> Failed: protocol violation<br/>or local fault

    Completed --> Tombstone
    Canceled --> Tombstone
    TimedOut --> Tombstone
    Failed --> Tombstone

    Tombstone --> [*]: retained past the longest<br/>message expiry, then dropped

    note right of Tombstone
        Late or duplicate packets stay routable:
        acknowledged and ignored, except a
        re-issued cancellation, which is re-answered.
    end note
```

| Terminal | Trigger | Reaches the peer as |
| --- | --- | --- |
| `Completed` | Both producers sent `last`. | Its own `last` messages. |
| `Canceled` | Either side sent or received a `Canceled` (`499`) status. | The same `Canceled` message — it both initiates and confirms. |
| `TimedOut` | This side's exchange budget elapsed before completion. | Nothing. Timeout is purely local, and the peer times out independently. |
| `Failed` | A correlation-matched message broke the wire contract, or the local side faulted. | A `4xx`/`5xx` status message, or — for a local fault, which has no status of its own — a best-effort cancellation. |

## 3. Protocol workflow

One trace, from establishment through full duplex, then branching into each of the four ways an
exchange can end. `T` is the invoker's remaining exchange budget and `P` its client id.

```mermaid
sequenceDiagram
    autonumber
    participant IA as Invoker app
    participant IS as Invoker SDK
    participant B as MQTT broker
    participant ES as Executor SDK
    participant EA as Executor app

    rect rgb(235, 244, 255)
    Note over IA,EA: A. Establish - one correlation GUID, two topics, one exchange
    IA->>IS: InvokeStreamingCommand(request stream)
    IS->>B: SUBSCRIBE response topic clients/invokerId/...
    IS->>B: PUBLISH request[0]<br/>__stream=d:0:T, $partition=P
    B->>ES: request[0] - shared subscription picks one executor
    ES->>EA: OnStreamingCommandReceived(requests, stream metadata, exchange)
    IS-->>IA: response stream + exchange handle
    Note over IA,IS: Returns once request[0] is published,<br/>not when the request stream ends
    end

    rect rgb(237, 248, 237)
    Note over IA,EA: B. Full duplex - the two directions progress independently
    EA-->>ES: yield response[0]
    ES->>B: PUBLISH response[0]<br/>__stream=d:0
    B->>IS: response[0]
    IS-->>IA: deliver response[0], index 0
    IA->>IS: yield request[1]
    IS->>B: PUBLISH request[1]<br/>__stream=d:1:T
    B->>ES: request[1]
    ES->>EA: deliver request[1], index 1
    Note over IS,ES: Consumers de-dup on correlationId + index + timestamp<br/>QoS 1 may redeliver
    end

    Note over IA,EA: C. The exchange ends in exactly one of four ways

    alt Graceful - each producer closes its own stream
        IA->>IS: end request stream
        IS->>B: last request<br/>__stream=c:2:last:T, no payload, $partition=P
        B->>ES: last request
        ES->>EA: request stream ended
        Note over IS,ES: One stream closed - control still flows
        EA-->>ES: end response stream
        ES->>B: last response<br/>__stream=c:1:last, no payload
        B->>IS: last response
        IS-->>IA: response stream ended - exchange Completed
    else Cancellation - either side, at any time
        EA->>ES: cancel - the invoker may cancel instead, it is symmetric
        ES->>B: Canceled<br/>__stream=s:0, __stat=499
        B->>IS: Canceled
        IS-->>IA: exchange Canceled
        IS->>B: Canceled<br/>__stream=s:0:T, __stat=499, $partition=P
        B->>ES: Canceled
        Note over IS,ES: The same message both initiates and confirms,<br/>so a received Canceled always means the exchange is over
    else Timeout - the exchange budget elapses
        Note over ES,EA: Executor stalls or crashes
        Note over IS: Budget T elapses before both streams close
        IS-->>IA: exchange TimedOut
        Note over IS,ES: Purely local - no timeout message is ever sent,<br/>the peer reaches its own timeout independently
    else Protocol violation - a malformed in-exchange message
        IS->>B: request[2]<br/>__stream=d:2:T, payload cannot be deserialized
        B->>ES: request[2]
        ES->>B: status about request[2]<br/>__stream=s:2, __stat=4xx
        B->>IS: status
        IS-->>IA: exchange Failed - protocol error at index 2
        ES->>EA: exchange ended
    end

    Note over IA,EA: Whatever the ending, each side keeps a tombstone so that late<br/>or duplicate packets are acknowledged and ignored, never re-started
```

Reading notes for section **A**: the invocation returns as soon as the mandatory first request is
published — not when the request stream ends. Otherwise a full-duplex application deadlocks, because
request *n+1* may depend on response *n*, which the app cannot read until the call returns.

Reading notes for section **C**: only *cancellation* and *protocol violation* put anything on the wire
to end the exchange. Graceful completion is just both `last` messages having arrived, and timeout is
never announced at all. A **protocol violation** means a correlation-matched message broke the wire
contract — an undeserializable payload, a malformed `__stream`, an incompatible protocol version, a
QoS 0 publish, or a sequencing break such as data after `last`. **Application** errors are out of
scope: an app that wants to report one sends it as an ordinary data entry.

## Where to go next

| You want | Go to |
| --- | --- |
| The normative decision, grammar, and rationale | [ADR 25](0025-rpc-streaming.md) |
| Per-scenario traces: recovery, re-issued cancellation, packet classification | [Lifecycle diagrams](0025-rpc-streaming-lifecycle-diagrams.md) |
| An illustrative C# API shape | [ADR 25 appendix](0025-rpc-streaming.md#illustrative-net-api) |
| How unary RPC does the same job today | [RPC protocol reference](../../reference/rpc-protocol.md) |
