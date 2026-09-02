# ADR 33: Large Message Chunking

## Context

A broker advertises a Maximum Packet Size in CONNACK, and MQTT 5 §2.1.4 counts the **whole PUBLISH**
against it — topic, properties and payload, not just payload. AIO RPC payloads can exceed that
limit, and today such an invocation simply fails, leaving each application to invent its own
fragmentation.

Scope is RPC request and response.

## Requirements

* A request or response whose encoded PUBLISH exceeds the usable packet limit MUST still be
  delivered, by splitting it across several PUBLISHes and reassembling it before the application
  sees it.
* Reassembly MUST reproduce the original message exactly — the payload byte for byte, and the user
  properties the sender set, in their original order.
* Chunking MUST be transparent. The command API, generated code and application handlers are
  unchanged, and a chunked invocation MUST be indistinguishable from an unchunked one to the caller.
* Every chunk of a message MUST reach the same executor, so that a shared subscription with several
  executors cannot scatter one message across them.
* A chunk MUST NOT be acknowledged until the reassembled message has been delivered, so that a
  transfer interrupted midway is redelivered rather than silently lost.
* Reassembly MUST be bounded in memory and in time, and a message that never completes MUST be
  discarded with everything held for it released.
* A message that cannot be chunked at all MUST fail with a distinct error rather than being
  published as a packet the broker will discard.
* A chunked message MUST be reassemblable by any conforming implementation, whichever language
  split it.
* A chunk's format support MUST be implied by protocol version 2.0, with no per-transfer opt-out.
  During migration, a 1.0 request MUST be able to advertise that 2.0 responses are accepted.

## Non-requirements

* **Telemetry chunking.** Deferred. The buffer is deliberately free of RPC concepts so telemetry can
  reuse it later (§1), but nothing in this ADR applies to telemetry today.
* **Streaming.** A chunked message is one logical message whose size is known before the first chunk
  is published. Multi-entry exchanges are
  [ADR XX](https://github.com/Azure/iot-operations-sdks/blob/maxim/streaming-adr-2/doc/dev/adr/0025-rpc-streaming.md),
  which lists chunking as a non-requirement in return.
* **Cancelling a transfer in flight.** There is no backchannel. Once an executor begins publishing a
  chunked response the invoker cannot ask it to stop, and unsubscribing does not signal the peer; the
  transfer runs to completion or expires.
* **Incremental delivery.** The application receives the whole message or nothing. A partially
  reassembled payload is never surfaced, and there is no progress signal.
* **Resuming across a lost session.** If a session is lost mid-reassembly the partial state is
  discarded and the transfer fails. There is no cross-session resume.
* **Reassembly by peers that do not implement this protocol.** Chunk metadata is not a public
  contract for applications to reassemble themselves.
* **User-facing configuration.** No enable/disable switch and no chunk-size knob (§11).

## Decision

### 1. Layering

Splitting and reassembly happen in `CommandInvoker` and `CommandExecutor`. The reassembly buffer
sits between header validation and the response cache, so the cache only ever sees whole messages
and the rest of the pipeline works without modification.

The buffer itself is free of RPC concepts. Entries are keyed by `message_id` plus the MQTT operation
identity (topic, response topic and correlation data), so unrelated operations cannot collide even
if a peer reuses a UUID. The caller supplies the deadline, so telemetry can reuse the buffer later.

### 2. Wire format

One reserved user property, `__chunk` ([ADR 4](./0004-reserved-user-properties.md)), colon-separated
and introduced by a tag so the parser never infers the shape from the field count:

```txt
chunk_metadata         ::= response_chunk | request_chunk
response_chunk         ::= head_chunk | property_chunk | data_chunk
request_chunk          ::= head_request_chunk | property_request_chunk | data_request_chunk
head_chunk             ::= "h" ":" message_id ":" chunk_index ":" total_chunks ":" checksum_id ":" checksum
property_chunk         ::= "p" ":" message_id ":" chunk_index ":" total_chunks
data_chunk             ::= "d" ":" message_id ":" chunk_index ":" total_chunks
head_request_chunk     ::= head_chunk ":" remaining_seconds
property_request_chunk ::= property_chunk ":" remaining_seconds
data_request_chunk     ::= data_chunk ":" remaining_seconds
```

| Field | Type | Presence |
|---|---|---|
| `message_id` | UUID, 8-4-4-4-12 | every chunk |
| `chunk_index` | uint, `0` on the head chunk | every chunk |
| `total_chunks` | uint `>= 1`, counting every chunk of the message | every chunk |
| `checksum_id` | token naming the algorithm | head chunk only |
| `checksum` | lowercase hex over the reassembled payload | head chunk only |
| `remaining_seconds` | positive uint operation countdown | every request chunk only |

UUIDs, integers and checksums use the canonical forms shown above: lowercase UUID `D` format,
unsigned decimal integers without leading zeroes, and lowercase hexadecimal checksums.

Index `0` must use the head form and the head form must be index `0`; either violation is a parse
failure. Property chunks occupy the indices immediately after the header and data chunks follow
them, so each chunk's role is known from its own tag and needs no boundary marker on the header.
Colon-separated rather than JSON because it rides on every chunk. Chunks may arrive out of order:
any chunk can create a reassembly-buffer entry keyed by `message_id`. Repeating `total_chunks` lets
the receiver validate the declared count and `chunk_index` before buffering that first arrival; all
chunks attributed to the entry must declare the same total. Completion requires the head and exactly
the indices `{0, ..., total_chunks - 1}`.

### 3. Chunk roles

Each chunk carries exactly one kind of thing:

| Chunk | Tag | Carries | Count |
|---|---|---|---|
| Header | `h` | message-level checksum metadata only — checksum identifier and checksum. No payload or logical user properties | exactly one, at index `0` |
| Property | `p` | a slice of the message's user property set | zero or more |
| Data | `d` | a slice of the payload | zero or more |

Every chunk additionally carries what routing and validation need on each packet — `$partition`,
`$high_priority`, `__protVer`, `__supProtMajVer` and `__chunk` itself. The message's user properties are the
concatenation, in index order, of those carried by the property chunks; its payload is the
concatenation, in index order, of the data chunks.

Within the MQTT User Property sequence, packet-level properties precede `__chunk`. On a property
chunk, the logical property slice follows `__chunk`; on a head or data chunk, `__chunk` is last.
Reassembly concatenates only the properties after `__chunk` on property chunks. This boundary
preserves names, duplicates and order without confusing repeated routing properties for logical
properties.

The splitter sizes each property-chunk candidate and an empty data-chunk probe instead of guessing
their MQTT overhead. This avoids an oversized PUBLISH that the broker may silently discard.

Splitting the property list is safe in a way splitting the payload is not: properties reassemble by
ordered concatenation, not a byte splice, so how a sender packs them need not match how another
implementation would. Only a single name/value pair that cannot fit in one packet makes a message
undeliverable, and that is reported as such.

The alternative — a per-implementation overhead constant — is rejected. It cannot be both safe and
efficient, and three languages would pick three different numbers.

### 4. Sizing

* The chunking trigger compares the **encoded packet size** against the limit, not payload length.
* Packet sizing is exact arithmetic over the MQTT 5 encoding and is **not** broker-specific; no
  resize-and-retry is needed. Implementations must pin it against their client's own serializer.
* The usable limit is the negotiated maximum **minus what the broker adds on delivery**. Sizing to
  exactly the negotiated value risks chunks being silently discarded: per \[MQTT-3.1.2-25\] a server
  that cannot send an oversized packet to a client **discards it and behaves as if it had completed
  sending**, so reassembly never completes and the caller sees only a timeout.
* Only two things can grow a packet in flight: topic aliases (disabled — `TopicAliasMaximum` is `0`)
  and subscription identifiers. Only subscriptions that **carried** an identifier in SUBSCRIBE
  contribute — +2..5 bytes each, one per matching identified subscription **on the receiving
  client's own session**, not per executor. The SDK assigns none, so the baseline is *publish size
  equals delivery size*, and it is provable rather than assumed whenever CONNACK reports
  `SubscriptionIdentifiersAvailable = false`.

#### Declared delivery allowance

The SDK does not stop an application subscribing with an identifier on the same connection its
envoys use, so the baseline above is a default, not a guarantee. The allowance is therefore
**declared**, in the unit that causes it:

| | |
|---|---|
| Setting | `MaxIdentifiedSubscriptionsPerDelivery`, default `0` |
| Scope | the **connection**, not the envoy — every envoy sharing a client must size against the same value, and it is surfaced through the same accessor that yields the negotiated maximum packet size |
| Effect | the chunk budget subtracts `count × 5` bytes: one property identifier byte plus a variable byte integer of up to four |
| Guard | the client conservatively counts live identified topic filters and rejects a SUBSCRIBE that would exceed the allowance, turning a silently discarded chunk into a startup error |

Rejected alternatives: a byte-valued `ReservedDeliveryOverheadBytes`, which is an underivable and
unauditable number of exactly the kind §3 removed for property overhead; and a fixed safety margin,
which is a bound in neither direction — any round number large enough to look reassuring is still
wasteful for the common case and short of the pathological one.

Known limitation: the quantity belongs to the **receiver** and is applied by the **sender**, which
cannot observe it, so this is a deployment-wide declaration rather than a per-peer fact. Making it
exact would mean advertising it on the wire, which is disproportionate to a handful of bytes.

### 5. Checksum

The head chunk names the algorithm that produced the checksum, so the receiver verifies with the
**sender's** algorithm. An identifier the receiver cannot resolve discards the message with a
distinct diagnostic rather than guessing, because verifying with the wrong algorithm produces a
mismatch indistinguishable from corruption. The ADR defines the registered set and its identifiers;
`sha256` is the baseline every implementation must support.

The checksum guards against **implementation error** — a slicing off-by-one producing a complete but
wrong payload, or two languages disagreeing about the split. Structure already covers the rest: a
missing chunk fails completion, ordering is fixed by index, repeated indices replace one retained
slot, unrelated operations cannot mix because the key includes MQTT operation identity, and bit
corruption is caught by TCP and TLS.

It is **not a security control and cannot become one** — it travels unauthenticated in a plaintext
user property beside the payload it describes, so anyone able to alter the payload can recompute it.
Choice of algorithm is therefore a performance decision, to be measured on target hardware; on x64,
SHA-256 is the fastest of the obvious candidates because of the hardware SHA extensions.

### 6. Timeouts — two clocks

Following ADR 25:

| Clock | Scope | Rule |
|---|---|---|
| Operation budget | the whole invocation | configured by the invoker; each side runs its own countdown and never resets it |
| `MessageExpiryInterval` | one chunk | set to the budget **remaining at the moment that chunk is published** |

So expiry shrinks across the sequence and no chunk outlives the invocation. A chunk with zero or
absent expiry is rejected, since nothing would bound how long a partial message is retained.

**Receive side:** the executor must not derive its reassembly deadline from `MessageExpiryInterval`
— that conflates a message clock with an operation clock, which ADR 25 explicitly rejects, and it
requires the *head* chunk to arrive first, which is not guaranteed. Every request-direction chunk
therefore appends a required positive countdown:
`h:...:total:checksum_id:checksum:remaining_seconds`,
`p:...:index:total:remaining_seconds` and `d:...:index:total:remaining_seconds`. The tag fixes the
form, so response-direction chunks omit the field without ambiguity; the invoker uses its own
absolute deadline.

MQTT expresses expiry in whole seconds. A sender rounds a positive fractional remainder up so a
usable final fraction is not encoded as the already-expired value `0`; a chunk can consequently
outlive the local deadline by less than one second, never by a full second.

Consequence to document: the invoke timeout now has to cover split, transmit, reassemble, execute,
respond and reassemble again. A timeout that worked for an unchunked payload can stop working once
the payload crosses the chunking threshold.

### 7. Bounds

The buffer is bounded from the outset, or a message that stalls after one chunk becomes an entry
nothing reclaims:

* a maximum chunk count per message, declared on every chunk and checked before that chunk is
  buffered — eventually derived from the negotiated receive maximum rather than a constant;
* a total reassembly budget across all in-flight messages;
* a maximum number of concurrently active reassembly entries, independent of their byte cost;
* a deadline **supplied by the caller**, never derived inside the buffer;
* a local maximum reassembly window that caps both caller and peer-provided deadlines;
* an autonomous expiry action scheduled when an entry is created, so cleanup does not depend on
  another chunk arriving.

The aggregate budget is admission control, not a reservation per operation: several individually
valid maximum-size transfers may not fit concurrently. The arrival that would exceed the budget is
rejected with `503`; existing entries retain their allocations until completion or expiry.

### 8. Acknowledgement

No chunk is acknowledged until the reassembled message has been handed to the user, or a crash
mid-reassembly silently loses data. Retaining each chunk's delivery context makes acknowledging the
reassembled message fan out to every chunk it was built from, so redelivery replays the whole
message. This is cheap wherever the client exposes that context; an implementation that cannot
retain it has to track the outstanding acknowledgements itself.

### 9. Error handling

Chunking has no backchannel of its own — a receiver can say nothing about an individual chunk. The
request direction does not need one: every chunk carries the correlation data and response topic, so
an executor that cannot reassemble answers with an **ordinary RPC error response** rather than
leaving the caller to time out. The response direction has no equivalent, and needs none: the
invoker is the final consumer and fails its own pending invocation.

**Rejected before the chunk is buffered:**

* `__chunk` is unparsable — unknown tag, the wrong field count for its tag, or a malformed message
  id or index.
* The head form appears at a non-zero index, or index `0` is not in the head form.
* `total_chunks` is zero or exceeds the local maximum.
* `chunk_index` is at or beyond `total_chunks`.
* The chunk carries no message expiry, or an expiry of zero (§6).

**Rejected once the chunk is attributed to a message:**

* Its `total_chunks` disagrees with the total already recorded for that `message_id`.
* The reassembly budget is exhausted (§7).
* A second head chunk disagrees with the first about `checksum_id` or `checksum`.
* The head chunk names a `checksum_id` the receiver cannot resolve (§5).
* A property chunk arrives at an index after a data chunk (§2).

**Rejected at completion, or when the deadline passes:**

* The checksum does not match the reassembled payload.
* The deadline passes with chunks still missing.

**Not errors.** A repeated index is an ordinary QoS 1 redelivery, and it is routine here rather than
exceptional: §8 leaves every chunk unacknowledged for the whole transfer, so a single reconnect
mid-message redelivers all of them. It is acknowledged and ignored, and only *conflicting* metadata
is a fault. Chunks arriving out of order are expected (§2). A chunk naming a message that has
already completed or been discarded is acknowledged and ignored.

Receivers retain a bounded tombstone for each completed or discarded operation through its deadline
and a short minimum grace period. This prevents late chunks from creating a new partial entry or a
second error response; count and retention bounds keep the tombstones from becoming unbounded state.

Two traps follow from that, both of which turn a healthy transfer into a failure:

* **The reassembly budget must not double-count.** Charging a redelivered chunk's bytes to the
  in-flight total a second time trips the bound in §7 and discards a message that was never too
  large.
* **A repeated index must not leak either delivery context.** The buffer retains the newest context
  for final acknowledgement and releases the displaced local context. An ordered-ack client must do
  so without acknowledging the newly delivered packet; otherwise the displaced queue entry can
  stall every acknowledgement behind it. One retained context per index is sufficient.

Two invariants hold across all of the above:

1. **Discarding a message means acknowledging every chunk held for it.** On a client that
   acknowledges in order, one unacknowledged chunk stalls every later acknowledgement on the
   connection — so a discard that forgets its held chunks wedges the client instead of failing one
   invocation.
2. **Every discard is attributable.** The message id, the chunk index and the reason are reported,
   because a discarded message is otherwise indistinguishable from network loss.

**What the peer is told.** In the request direction the executor publishes an error response on the
correlation data and response topic that every chunk carries: `400` for malformed metadata or a
checksum mismatch, `408` for a message still incomplete at its deadline, and `503` where local
bounds refused a well-formed message this executor could not accept. A chunk without correlation
data or a response topic is discarded silently, as the existing header validation already requires.
In the response direction nothing is published; the invoker fails the invocation locally.

Existing error kinds carry these failures across languages: malformed metadata and checksum
mismatches map to `PayloadInvalid`/`400`, incomplete reassembly to `Timeout`/`408`, and exhausted
local capacity to `StateInvalid`/`503`. A distinct sender-side error identifies a message that
cannot be represented within the packet and chunk limits. Error responses use a separate bounded
expiry so reporting a deadline failure does not create a response that never expires.

### 10. Disconnection and recovery

Chunking inherits the [session client](../../reference/session-client.md)'s recovery semantics and
leans on them harder than vanilla RPC does, because §8 leaves every chunk of a transfer
unacknowledged until the whole message is complete.

**A session that survives.** The session client requires a non-zero session expiry interval, so a
reconnect resumes the session rather than starting a new one. Nothing was acknowledged, so the
broker still holds every chunk it delivered and re-sends them all under their original packet
identifiers. The receiver replaces the delivery context it holds for each index (§9) and reassembly
continues rather than restarting: the payload already buffered stays valid, and only the
acknowledgement handles change. The reassembly deadline is **not** extended, because it belongs to
the operation rather than to the connection.

Individual chunks can still be lost across the outage. The broker decrements each message's expiry
by the time it spent waiting, so a long enough outage retires chunks before the session resumes. The
message then cannot complete and its remainder is discarded at the deadline.

On the sending side, chunks not yet published are queued and flush on reconnect, each carrying the
budget remaining when it is finally published (§6). A reconnect that consumes the budget therefore
stops the transfer and fails the invocation naming the chunk it reached, rather than emitting chunks
that are already expired.

**A session that is lost.** If the session expires or is started clean, the buffered chunks are never
redelivered and the partial message can never complete. It holds its share of the reassembly budget
until its deadline and is then discarded (§7). Acknowledging its retained delivery contexts at that
point achieves nothing, since the acknowledgements they referred to went with the connection — which
is harmless, and is why discarding must not depend on acknowledgement succeeding.

**An executor that is lost.** A chunked message can only be reassembled by the endpoint that
received its chunks: partial state lives in memory keyed on `message_id` and is not shared between
members of a shared subscription. A message is therefore recoverable only as a whole. Whether a
broker redistributes a terminated member's unacknowledged messages to another member is
implementation-defined; where it does, that member holds a partial set it cannot complete and both
halves expire at their deadlines. Either way the invoker sees a failure within its budget rather
than a wrong result. This is where chunking differs from streaming, which recovers mid-exchange
because each entry stands alone.

**Two costs to accept:**

* **A reconnect replays the whole transfer.** Deferring every acknowledgement means a disconnect at
  95% of a 10 MB message redelivers all 10 MB. That is the price of the guarantee in §8.
* **A link that reconnects faster than a transfer completes never completes it.** Each reconnect
  restarts delivery of the entire message, so where the mean time between reconnects is shorter than
  the transfer takes, progress is zero and the operation fails at its deadline with no partial
  result. Chunking makes a large message deliverable on a stable link; it does not make an unstable
  link usable.

### 11. Versioning and configuration

* Chunked RPC messages use wire protocol **2.0**. Chunking is implied by that version — no feature
  negotiation and no opt-out. A 2.0 implementation that rejects chunked messages is non-compliant.
* During migration, unchunked traffic remains on 1.0. A large request uses 2.0 and receives a
  version error from a legacy executor rather than being misinterpreted. Every request advertises
  acceptable response majors in `__supProtMajVer`; a chunk-aware invoker advertises `1 2`. This
  lets a chunk-aware executor answer a small 1.0 request with either an unchunked 1.0 response or,
  when needed, a chunked 2.0 response. Without that advertisement, an oversized response returns
  `503` rather than incompatible chunks.
* Chunking is **automatic and opaque**: no user-facing chunk-size knob and no enable/disable
  setting.
* QoS is preserved across all chunks, and all chunks use the same topic.

## Alternatives Considered

**Chunking below the envoys, as a decorator over the MQTT client**, splitting on publish and
reassembling on receive so the RPC layer never knows. Rejected, because each of these is invisible
from the transport layer:

* **Protocol version.** Chunking is part of the RPC wire protocol and has to be gated on it, but the
  version is an RPC-level user property the transport does not see. A 1.0 legacy path would be
  impossible to express.
* **Invocation budget.** A chunk's expiry is the budget remaining when it is published (§6). Only
  the envoy holds that; a decorator sees one already-flattened `MessageExpiryInterval`.
* **Acknowledgement.** Deferring the ack until the handler has run is envoy policy. A decorator
  would have to fabricate a delivery context whose acknowledgement fans out to every chunk,
  reimplementing envoy behaviour underneath the envoy.
* **Scope.** The decorator wraps a connection shared with every other envoy and with the
  application, so chunking could not be confined to RPC traffic.

Sizing is genuinely easier at that layer, sitting next to the encoded packet, but it does not
outweigh the above.

## Open questions

1. **Should a chunked response be cacheable at all?** A cached-response replay is re-chunked and
   re-sent in full, with no way for the invoker to decline it (there is no mid-transfer backchannel).
2. **Cache memory.** A reassembled multi-MB payload enters the response cache exactly as a large
   unchunked one would; bounding *reassembly* does nothing about that.
3. **Prerequisite, not part of this ADR.** The negotiated maximum packet size is unreachable from
   any envoy in all three languages. That plumbing should land first so this protocol can assume it
   exists.
4. **Cross-language dedupe divergence.** Rust delays the ack, .NET coalesces onto the same task, Go
   drops the duplicate. This must be reconciled before the METL tests can be written.

## Consequences

* Payloads above the broker limit become deliverable through the ordinary command API, with no
  application-level fragmentation and no change to codegen output.
* Two extra messages per chunked transfer in the common case — the header chunk and one property
  chunk — repaid in part by a larger and provably correct payload budget per data chunk.
* The delayed-ack window widens to the whole transfer, interacting with ordered acknowledgement and
  the executor's dispatcher concurrency.
* A request that cannot be reassembled is answered with an error response rather than silence, so
  the caller learns the outcome within the invocation budget instead of at its expiry. A response
  that cannot be reassembled has no such path and still fails at the invoker.
* [rpc-protocol.md](../../reference/rpc-protocol.md) and
  [protocol-versioning.md](../../reference/protocol-versioning.md) need updating for the 2.0 bump.

## Appendix

[Large Message Chunking Diagrams](./0033-large-message-chunking-diagrams.md) illustrates the
layering, the wire format, the happy path, how a receiver classifies a chunk, and what a reconnect
does to a transfer in progress.
