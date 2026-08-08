# ADR 33: Large Message Chunking

> **Status:** Draft.
> Supersedes the unmerged draft numbered *ADR 23 — Large Message Chunking in MQTT Protocol*
> (branch `maxim/chunking`); `0023` is already taken by
> [0023-property-support.md](./0023-property-support.md).
> Backed by a .NET POC — decisions and their derivations are in
> [rpc-chunking-poc-plan.md](../rpc-chunking-poc-plan.md), the code walkthrough in
> [rpc-chunking-implementation-walkthrough.md](../rpc-chunking-implementation-walkthrough.md), and
> the review that preceded it in [rpc-chunking-working-doc.md](../rpc-chunking-working-doc.md).

## Context

A broker advertises a Maximum Packet Size in CONNACK, and MQTT 5 §2.1.4 counts the **whole PUBLISH**
against it — topic, properties and payload, not just payload. AIO RPC payloads can exceed that
limit, and today such an invocation simply fails, leaving each application to invent its own
fragmentation.

An earlier draft put chunking in an `IMqttPubSubClient` decorator. That is rejected: chunks bypass
correlation, the response cache and dedupe. Chunking belongs **inside the RPC envoys**, above the
cache, so codegen and correlation keep working unchanged.

Scope is RPC request and response. Telemetry chunking is deferred. Streaming is a separate mechanism
([ADR 25](https://github.com/Azure/iot-operations-sdks/blob/maxim/streaming-adr-2/doc/dev/adr/0025-rpc-streaming.md)),
which lists chunking as a non-requirement.

## Decision

### 1. Layering

Splitting and reassembly happen in `CommandInvoker` and `CommandExecutor`. The reassembly buffer
sits between header validation and the response cache, so the cache only ever sees whole messages.
Placing it below the cache would deadlock on the golden path: every chunk shares
`(responseTopic, correlationData)`, so chunk 1 looks like an in-flight duplicate of chunk 0 and
awaits a placeholder that is never completed.

The buffer itself is free of RPC concepts — keyed on `messageId`, given its deadline by the caller —
so telemetry can reuse it later.

### 2. Wire format

One reserved user property, `__chunk` ([ADR 4](./0004-reserved-user-properties.md)), colon-separated
and introduced by a tag so the parser never infers the shape from the field count:

```txt
chunk_metadata ::= head_chunk | data_chunk
head_chunk     ::= "h" ":" message_id ":" chunk_index ":" total_chunks ":" checksum_id ":" checksum
data_chunk     ::= "d" ":" message_id ":" chunk_index
```

| Field | Type | Presence |
|---|---|---|
| `message_id` | UUID, 8-4-4-4-12 | every chunk |
| `chunk_index` | uint, `0` on the head chunk | every chunk |
| `total_chunks` | uint `>= 1`, counting the head chunk | head chunk only |
| `checksum_id` | token naming the algorithm | head chunk only |
| `checksum` | lowercase hex over the reassembled payload | head chunk only |

Index `0` must use the head form and the head form must be index `0`; either violation is a parse
failure. Colon-separated rather than JSON because it rides on every chunk. Out-of-order delivery is
fine — whichever chunk arrives first creates the entry, and completion is "total known and all
indices present".

### 3. The head chunk carries properties, not payload

Chunk 0 carries the full user property set and **zero payload**. Chunks 1..n carry payload plus only
the properties reassembly and routing need (`$partition`, `$high_priority`, `__protVer`).

This is what makes the chunk size *measurable* instead of guessed. A data chunk's property set is
entirely SDK-controlled, so its overhead is computed exactly; the unbounded, caller-controlled part
is confined to one message that merely has to fit in one packet — it cannot be split across chunks
anyway, so a property set that does not fit is an undeliverable message and is reported as one.

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
| Guard | a client used by envoys rejects a SUBSCRIBE carrying an identifier unless the allowance covers it, turning a silently discarded chunk into a startup error |

Rejected alternatives: a byte-valued `ReservedDeliveryOverheadBytes`, which is an underivable and
unauditable number of exactly the kind §3 removed for property overhead; and a fixed safety margin,
which is not a bound in either direction — 64 bytes is thirteen times what a realistic deployment
needs and still short of a pathological one.

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
missing chunk fails completion, ordering is fixed by index, duplicate indices are rejected, messages
cannot mix because the buffer is keyed on `messageId`, and bit corruption is caught by TCP and TLS.

It is **not a security control and cannot become one** — it travels unauthenticated in a plaintext
user property beside the payload it describes, so anyone able to alter the payload can recompute it.
Choice of algorithm is therefore a performance decision, to be measured on target hardware; on x64,
SHA-256 is the fastest of the obvious candidates because of the hardware SHA extensions.

### 6. Timeouts — two clocks

Following ADR 25 rather than the earlier draft:

| Clock | Scope | Rule |
|---|---|---|
| Operation budget | the whole invocation | configured by the invoker; each side runs its own countdown and never resets it |
| `MessageExpiryInterval` | one chunk | set to the budget **remaining at the moment that chunk is published** |

So expiry shrinks across the sequence and no chunk outlives the invocation. A chunk with zero or
absent expiry is rejected, since nothing would bound how long a partial message is retained.

**Receive side:** the executor must not derive its reassembly deadline from `MessageExpiryInterval`
— that conflates a message clock with an operation clock, which ADR 25 explicitly rejects, and it
requires the *head* chunk to arrive first, which is not guaranteed. `__chunk` therefore carries an
optional trailing countdown on request-direction chunks —
`h:...:checksum[:remainingSeconds]` and `d:...:index[:remainingSeconds]`, unambiguous because the
tag already fixes the form. Response-direction chunks omit it; the invoker uses its own budget.

Consequence to document: the invoke timeout now has to cover split, transmit, reassemble, execute,
respond and reassemble again. A timeout that worked for an unchunked payload can stop working once
the payload crosses the chunking threshold.

### 7. Bounds

The buffer is bounded from the first line of code, or a message that stalls after one chunk becomes
an entry nothing reclaims:

* a maximum chunk count per message, checked the moment the head chunk names the total, discarding
  the message and releasing every chunk held for it — eventually derived from the negotiated receive
  maximum rather than a constant;
* a total reassembly budget across all in-flight messages;
* a deadline **supplied by the caller**, never derived inside the buffer.

### 8. Acknowledgement

No chunk is acknowledged until the reassembled message has been handed to the user, or a crash
mid-reassembly silently loses data. Retaining each chunk's delivery context makes acknowledging the
reassembled message fan out to every chunk it was built from, so redelivery replays the whole
message. This proved cheap in .NET; Rust and Go should confirm before assuming otherwise.

### 9. Versioning and configuration

* RPC wire protocol bumps to **2.0**. Chunking is implied by the version — no feature negotiation,
  no opt-out. A 2.0 implementation that rejects chunked messages is non-compliant.
* Chunking is **automatic and opaque**: no user-facing chunk-size knob and no enable/disable
  setting. The earlier draft's configuration setting is dropped.
* QoS is preserved across all chunks, and all chunks use the same topic.

## Open questions

1. **Error kinds.** Which existing kind absorbs incomplete-chunk, checksum-mismatch and
   buffer-limit failures, versus adding new ones — `AIOProtocolErrorKind` is not `#[non_exhaustive]`,
   so a new variant is a Rust breaking change. Reusing `PayloadInvalid` or `HeaderInvalid` is
   cheapest and costs diagnostics. Until this is settled, a failed reassembly surfaces only as a
   caller timeout with nothing implicating chunking.
2. **Should a chunked response be cacheable at all?** A cached-response replay is re-chunked and
   re-sent in full, with no way for the invoker to decline it (there is no mid-transfer backchannel).
3. **Cache memory.** A reassembled multi-MB payload enters the response cache exactly as a large
   unchunked one would; bounding *reassembly* does nothing about that.
4. **Prerequisite, not part of this ADR.** The negotiated maximum packet size is unreachable from
   any envoy in all three languages. That plumbing should land first so this protocol can assume it
   exists.
5. **Cross-language dedupe divergence.** Rust delays the ack, .NET coalesces onto the same task, Go
   drops the duplicate. This must be reconciled before the METL tests can be written.

## Consequences

* Payloads above the broker limit become deliverable through the ordinary command API, with no
  application-level fragmentation and no change to codegen output.
* One extra message per chunked transfer (the header chunk), repaid in part by a larger and provably
  correct payload budget per data chunk.
* The delayed-ack window widens to the whole transfer, interacting with ordered acknowledgement and
  the executor's dispatcher concurrency.
* Failures inside a transfer are, until open question 1 is settled, indistinguishable from a plain
  timeout at the caller.
* [rpc-protocol.md](../../reference/rpc-protocol.md) and
  [protocol-versioning.md](../../reference/protocol-versioning.md) need updating for the 2.0 bump.
