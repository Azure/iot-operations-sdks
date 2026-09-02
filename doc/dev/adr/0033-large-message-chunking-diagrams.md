# Large Message Chunking Diagrams

Companion to [ADR 33](./0033-large-message-chunking.md). Each diagram illustrates one decision in
that document and adds nothing to it.

## 1. Where chunking sits

Splitting and reassembly live inside the envoys, above the response cache, so the cache and the
correlation map only ever see whole messages.

```mermaid
flowchart TB
    App["Application or generated client"]

    subgraph Inv["CommandInvoker"]
        direction TB
        IS["Splitter<br/>sizes the packet, emits h / p / d"]
        IB["ChunkBuffer<br/>reassembles the response"]
    end

    subgraph Exec["CommandExecutor"]
        direction TB
        EV["Header validation<br/>runs on every chunk"]
        EB["ChunkBuffer<br/>reassembles the request"]
        EC["CommandResponseCache<br/>only ever sees whole messages"]
        EH["User handler"]
        ES["Splitter<br/>splits the response"]
    end

    B[("MQTT broker")]

    App --> IS
    IS --> B
    B --> EV
    EV --> EB
    EB --> EC
    EC --> EH
    EH --> ES
    ES --> B
    B --> IB
    IB --> App

    classDef key fill:#e6f3ff,stroke:#0066cc,stroke-width:2px
    class IS,IB,EB,ES key
```

## 2. What one message becomes

A header chunk carries the message-level metadata, property chunks carry the user property set, and
data chunks carry the payload. Every chunk additionally carries what routing and per-chunk
validation need.

```mermaid
flowchart LR
    M["One logical message<br/>user properties plus payload"]

    M --> H["h:message_id:0:5:sha256:hash<br/>message id, chunk index, total,<br/>checksum id, checksum<br/>no logical properties, no payload"]
    M --> P["p:message_id:1:5<br/>message id, chunk index, total<br/>a slice of the user properties<br/>zero or more"]
    M --> D1["d:message_id:2:5<br/>message id, chunk index, total<br/>a slice of the payload"]
    M --> D2["d:message_id:3:5<br/>message id, chunk index, total<br/>a slice of the payload"]
    M --> D3["d:message_id:4:5<br/>message id, chunk index, total<br/>a slice of the payload"]

    H --> R["Reassembled message"]
    P --> R
    D1 --> R
    D2 --> R
    D3 --> R

    R --> N["properties = ordered concatenation of the p chunks<br/>payload = ordered concatenation of the d chunks"]

    E["Every chunk also carries<br/>partition, high priority, protocol version, chunk metadata"]
    E -.-> H
    E -.-> P
    E -.-> D1
    E -.-> D2
    E -.-> D3
```

## 3. Happy path on the wire

Both directions split independently. Each chunk's expiry is the invocation budget remaining when
that chunk is published, so the values shrink across the sequence.

```mermaid
sequenceDiagram
    autonumber
    participant A as App
    participant I as CommandInvoker
    participant B as Broker
    participant E as CommandExecutor
    participant H as Handler

    A->>I: InvokeCommandAsync(request, timeout 30s)
    Note over I: encoded packet exceeds the limit<br/>measure a data chunk, then split
    I->>B: h:message_id:0:5:sha256:e3b0:30 - expiry 30
    I->>B: p:message_id:1:5:29 user properties - expiry 29
    I->>B: d:message_id:2:5:29 payload - expiry 29
    I->>B: d:message_id:3:5:28 payload - expiry 28
    I->>B: d:message_id:4:5:28 payload - expiry 28
    B->>E: all five chunks, same topic and correlation
    Note over E: headers validated per chunk<br/>chunks buffered, nothing acknowledged
    Note over E: head and all declared indices present<br/>checksum verified, message rebuilt
    E->>H: OnCommandReceived(request)
    H-->>E: response
    Note over E: cache stores the whole response
    E->>B: response chunks, expiry = budget remaining
    E->>B: PUBACK for all five request chunks
    B->>I: response chunks
    Note over I: reassemble and verify, then acknowledge<br/>one ack fans out to every chunk
    I-->>A: ExtendedResponse
```

## 4. How a receiver classifies a chunk

Discarding a chunk releases that one packet. Discarding a message releases every chunk held for it,
which is what keeps a client that acknowledges in order from stalling.

```mermaid
flowchart TB
    C["chunk arrives"] --> X{"chunk metadata parses?"}
    X -->|no| DC["discard the chunk<br/>acknowledge it"]
    X -->|yes| Y{"message expiry present?"}
    Y -->|no| DC
    Y -->|yes| T{"operation recently terminated?"}
    T -->|yes| DC
    T -->|no| Z{"index already held?"}
    Z -->|yes| RP["retain the new delivery context<br/>release the displaced local context"]
    Z -->|no| BD{"within the count and size bounds?"}
    BD -->|no| DM["discard the message<br/>acknowledge every chunk held"]
    BD -->|yes| CP{"head and all declared indices present?"}
    CP -->|no| W["hold and wait"]
    CP -->|yes| CK{"checksum matches?"}
    CK -->|no| DM
    CK -->|yes| OK["deliver the reassembled message<br/>acknowledging it acknowledges every chunk"]
    W -->|deadline passes| DM

    classDef bad fill:#ffe6e6,stroke:#cc0000,stroke-width:2px
    classDef good fill:#e6ffe6,stroke:#009900,stroke-width:2px
    class DC,DM bad
    class OK good
```

## 5. Reconnect during a transfer

Because no chunk is acknowledged until the message completes, a reconnect redelivers the whole
transfer. Reassembly continues rather than restarting: the payload already buffered stays valid and
only the acknowledgement handles are replaced.

```mermaid
sequenceDiagram
    autonumber
    participant B as Broker
    participant E as Executor
    participant F as ChunkBuffer

    B->>E: chunk 0
    E->>F: hold, do not acknowledge
    B->>E: chunk 1
    E->>F: hold, do not acknowledge

    Note over B,E: connection lost<br/>pending acknowledgements are discarded
    Note over B: broker still holds 0 and 1<br/>because nothing was acknowledged
    Note over B,E: session resumes

    B->>E: chunk 0 again, original packet id
    E->>F: replace index 0 context<br/>retire the displaced local context
    B->>E: chunk 1 again, original packet id
    E->>F: replace index 1 context<br/>retire the displaced local context
    B->>E: chunk 2
    E->>F: hold - the message is now complete

    Note over F: verify the checksum and rebuild
    E->>B: PUBACK 0, 1 and 2, after delivery
```

## Coverage

| Diagram | Illustrates |
|---|---|
| 1 | Layering, and why the buffer sits above the cache |
| 2 | Wire format and chunk roles |
| 3 | Sizing, timeouts and acknowledgement on the happy path |
| 4 | Error handling, including what is deliberately not an error |
| 5 | Disconnection and recovery |
