# ADR 34: Chunking RPC

> **Draft — everything below `MQTT layer protocol` is still an outline.**
> Implements option C from [ADR 33](./0033-large-message-chunking-options.md).

## Context

MQTT caps packet size per connection: the broker declares its limit in `CONNACK`, and that limit varies with the broker's deployment profile. A payload above the cap cannot be published, and a packet too large for a subscriber to accept is discarded by the broker without notice — so oversized messages fail invisibly today.

Known scenarios already exceed the cap: WASM module images, Schema Registry schemas, captured media, and batched asset discovery. Schema Registry alone shows both shapes — `getSchema` is a small request with a large response, `putSchema` is large in both directions. All are commands — one request, one response — so this ADR covers RPC only.

[ADR 33](./0033-large-message-chunking-options.md) selected option C: chunking as its own envoy pair over its own wire protocol, independent of [RPC streaming](./0025-rpc-streaming.md). Streaming remains the answer for arbitrary-length, bidirectional exchanges; chunking moves one large payload and verifies it arrived whole.

## Requirements

 - A command request or response whose serialized payload exceeds the connection's maximum packet size MUST still be delivered — in either direction, or both within one invocation.
 - The application MUST receive the whole payload as its declared, deserialized type. Fragment boundaries MUST NOT be visible to it, and it MUST NOT have to reason about MQTT packet sizes.
 - Fragment size MUST be derived at run time from the connection's negotiated maximum packet size.
 - An incomplete or corrupted transfer MUST fail with a distinct, catchable error. A partial payload MUST NEVER be surfaced as complete.
 - Either side MUST be able to abort an in-flight transfer, and the peer MUST learn promptly rather than waiting out the timeout.
   - An executor that receives fragments for a transfer it holds no state for tells the invoker to start over; an abandoned transfer releases its reassembly buffer at once.
 - All fragments of one payload MUST reach the same executor under a shared subscription, and redelivered fragments MUST NOT cause a command to execute twice.
 - A consumer MUST bound the memory it commits to reassembly.
 - Peers that predate chunking MUST keep working unchanged; a recompile MAY be required to *use* chunking.

## Non-requirements

 - Resuming a partially transferred payload after a peer restarts or loses its session.
   - The transfer fails and the application re-invokes the command; confirmed acceptable in the ADR 25 review.
 - Chunked telemetry.
   - The fragment framing is one-way and can be reused for telemetry later; this ADR does not specify it.
 - Streaming semantics — arbitrary-length, interleaved, or independently typed entries.
   - A transfer carries exactly one payload per direction, and fragments are opaque byte ranges with no standalone meaning. See [ADR 25](./0025-rpc-streaming.md).
 - Progress reporting or partial delivery to the application.
   - A transfer is all-or-nothing.
 - Compressing payloads, or moving bytes off MQTT entirely.
   - Both stay application choices; ADR 33 records the claim-check pattern as the escape hatch for media-scale payloads.

## Decision

### Conceptual model

#### Chunked transfer

A **transfer** moves one serialized payload as an ordered set of **chunks** sharing one transfer identity. A command invocation carries two independent transfers — request and response — and either, both, or neither may be chunked.

Chunking engages **only when the serialized payload exceeds the limit**; a payload that fits is published as a single message, as unary mRPC does today. That is what keeps existing peers working.

The two transfers are **sequential, not interleaved** — the executor does not run the command until the request transfer is complete.

#### Chunks and transfer metadata

Every chunk carries the transfer identity, its index, and the **total chunk count and payload length**, repeated on each chunk rather than sent once. Repetition costs a few bytes and lets the consumer enforce its memory bound from whichever chunk arrives first, instead of buffering blind until chunk 0 shows up.

The integrity value is carried once — it is needed only at verification, and repeating a hash across tens of thousands of chunks is not free.

#### Transfer completion

A transfer completes when every index `0..N-1` has arrived; the consumer then verifies integrity, deserializes, and delivers one payload. Completion is all-or-nothing.

Because the total is on every chunk, the end of a transfer is self-evident and there is **no `last` control message**

A transfer that cannot complete — a chunk still missing at the deadline, an integrity mismatch, an abort from the peer, or a declared length over the consumer's bound — fails as a whole, releases its buffers, and surfaces a distinct error.

#### Producer behavior

Serialize first, then decide: if the payload fits, publish it unchanged; otherwise split it into chunks no larger than the limit and publish them under one transfer identity at QoS 1.

Chunks go out in index order, but ordered delivery is not relied upon — reassembly is by index. The producer does not wait for the consumer to acknowledge each chunk. If publication fails partway, it aborts the transfer and tells the peer.

#### Consumer behavior

On the first chunk of an unknown transfer the consumer allocates reassembly state, rejecting the transfer at once if the declared length exceeds its bound. It buffers by index and discards duplicates, which QoS 1 redelivery makes routine.

A chunk for a transfer it holds no state for — already completed, already failed, or addressed to an executor that has since restarted — is answered by telling the producer the transfer is incomplete, so it restarts instead of publishing the remainder into a void.

### MQTT layer protocol

#### Chunking user property

Each chunk carries a `__chunk` MQTT user property:

```txt
<property_value> ::= <chunk_index> ":" <chunk_count> ":" <payload_length> [ ":" <integrity> ]
<chunk_index>    ::= <uint>
<chunk_count>    ::= <uint>
<payload_length> ::= <uint>
<integrity>      ::= <hex32>
```

**Table 1. `__chunk` value fields.** There is only one kind of chunk message, so — unlike [ADR 25](./0025-rpc-streaming.md)'s tagged `d`/`c`/`s` forms — the value needs no discriminator.

| Field | Type | Meaning |
| ---- | ---- | ---- |
| `chunk_index` | uint | Position of this chunk within the transfer, `0`-based. |
| `chunk_count` | uint | Total chunks in the transfer. Repeated on every chunk. |
| `payload_length` | uint | Length in bytes of the whole serialized payload. Repeated on every chunk. |
| `integrity` | hex32 | CRC-32C of the whole serialized payload. Present on chunk `0` only, and optional. |

Examples: `0:5:4194304:1a2b3c4d` — first of five chunks of a 4 MiB payload, carrying the integrity value; `3:5:4194304` — the fourth chunk of that same transfer.

The transfer needs **no identifier of its own**: the invocation's correlation data plus the direction (request topic or response topic) already names it. ADR 23 minted a `messageId` because it chunked below the envoys, where no correlation exists; at the envoy layer it is redundant.

Every chunk except the last is **exactly the same size**, so a consumer can place any chunk at `chunk_index × chunk_size` without having seen its predecessors — `chunk_size` is simply the payload length of any non-final chunk, and the final chunk sits at `payload_length` minus its own length.

`__chunk` frames data only. Aborts and "I hold no state for this transfer" replies use the existing status headers; see [error handling](#error-handling).

CRC-32C rather than ADR 23's SHA-256: index completeness and `payload_length` already detect loss and truncation under QoS 1, so the integrity value only guards against corruption, and a cryptographic hash over tens of megabytes on an edge device buys nothing that TLS does not already provide.

#### Chunk sizing

Chunk size is computed per connection from the broker's `CONNACK` **Maximum Packet Size**, minus worst-case overhead — fixed and variable headers, topic name, correlation data, every reserved user property, and `__chunk` itself — with a conservative margin. It is recomputed on reconnect, since the value may change.

The publisher knows the broker's limit but **not the subscriber's**. A chunk that exceeds what the subscriber declared is discarded by the broker without notice, and the transfer then fails at its deadline. CoAP avoids this by letting the receiver shrink the block size mid-transfer; we have no equivalent without a handshake, so the mitigation is the conservative margin above.

#### User property propagation

Chunk `0` carries the **full** set of the original message's user properties. Every other chunk carries only what is needed to route, correlate and frame it: `__chunk`, correlation data, and `$partition`. Message expiry is an MQTT property and is set on all of them.

Repeating an application's user properties across thousands of chunks is pure overhead. The reassembled message therefore presents chunk `0`'s properties as its own.

#### Topics and routing

**Response transfers** flow on the invoker's response topic. That topic is unique to the invoker and is not shared-subscribed, so chunks need no routing help beyond correlation data.

**Request transfers** flow on a **distinct chunked command topic** — a reserved suffix on the ordinary command topic — which an executor that predates chunking never subscribes to. Two consequences follow:

- An old executor can never be handed a chunk it would try to deserialize as a whole payload.
- Publishing there when no chunk-capable executor is subscribed returns `no matching subscribers`, so the invoker learns the service cannot accept large requests **before** uploading the remaining chunks, rather than failing silently at the deadline. This mirrors ADR 25's `NoAvailableStreamingExecutor`.

Every chunk of a request transfer carries `$partition` set to the invoker's client id, so a shared subscription delivers all chunks of one transfer to the same executor. A request that fits goes to the ordinary command topic, unchanged.

#### Acknowledgement and de-duplication

All chunks are QoS 1. A consumer acknowledges each chunk **as it buffers it**, not when the transfer completes. Holding acknowledgements would consume the connection's Receive Maximum for the length of the whole transfer and stall it — the concern raised in the ADR 25 review about an executor that waits for a complete request before responding. Acknowledging early forfeits redelivery of already-acked chunks, which costs nothing here because an interrupted transfer is re-invoked rather than resumed.

De-duplication is by correlation data, direction and `chunk_index`; a repeated index is discarded.

The executor's existing replay cache continues to key on correlation data, so a fully redelivered request transfer resolves to one execution.

### Payload handling

#### Serialization boundary

#### Reassembly

#### Integrity verification

Completeness and length checks catch most of what can go wrong; the integrity value is the last line only.

| Failure | Caught by |
| ---- | ---- |
| A chunk never arrives | index completeness over `0..N-1` |
| The payload is truncated | reassembled length vs `payload_length` |
| A chunk is redelivered | index de-duplication |
| The headers disagree | `payload_length = (chunk_count - 1) × chunk_size + final chunk length` |
| Bytes altered in transit or in local buffers | `integrity` (CRC-32C) |
| The payload is deliberately tampered with | TLS, not this protocol |

Verification runs once a transfer is complete and before deserialization; a mismatch fails it like any other terminal error.

CRC-32C is a checksum, not a cryptographic hash — it detects every single-bit error and every burst error up to 32 bits, and it is linear and therefore trivially forgeable. It defends against accidents only, which is all that is needed while TLS authenticates the wire.

### Timeout support

#### Transfer level timeout

#### Chunk level timeout

### Resource limits

#### Reassembly bounds

#### Behavior when limits are exceeded

### Error handling

### Transfer termination

### Disconnection and recovery

### Binding to communication patterns

#### Chunked command invoker and executor

#### Chunked telemetry sender and receiver

#### Relationship to RPC streaming

### Compatibility and rollout

#### Coexistence with non-chunking peers

#### Topic and affordance strategy

### Modeling and code generation

### Protocol versioning

## Alternative designs considered

## Appendix

### Illustrative .NET API

### Message flow diagrams

### Maximum packet size assumptions
