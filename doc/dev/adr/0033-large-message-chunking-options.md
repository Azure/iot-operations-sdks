# ADR 33: Large Message Chunking — Implementation Options

> **Status: options analysis, no decision yet.** This document exists to frame the next design review. Each option below is written to be accepted or rejected on its merits; the recommendation at the end is a starting position, not a conclusion.

## Context

### What changed

[ADR 25](./0025-rpc-streaming.md) specifies RPC streaming as a general-purpose,
arbitrary-length, bidirectional exchange, and explicitly lists **chunking a single
large payload as a non-requirement**, on the grounds that any stream entry may be lost
and therefore a chunked payload could not be reliably reassembled.

The management review of ADR 25 reversed the priority order:

- **Chunking is the P0 scenario.** *"I think the chunking [scenarios] are P0… the one where you send a large payload which can't fit in one MQTT message. That's the primary scenario."*


### Named scenarios

| # | Scenario | Shape | Chunked direction | Status |
| --- | --- | --- | --- | --- |
| 1 | Large WASM image that does not fit one MQTT message | one small request → one large response | response | in scope |
| 3 | Camera / media capture too large for one MQTT message | one small request → one large response | response | in scope |
| 4 | Schema Registry `getSchema` for an oversized schema | one small request → one large response | response | in scope |
| 5 | Schema Registry `putSchema` for an oversized schema | one large request → one large response | **both** | in scope |



The problem reduces to: **an invoker and an executor must exchange request and response
payloads larger than the connection's maximum packet size, within one RPC operation.**

Three properties follow, and they hold for the rest of this document:

- **RPC-only.** No telemetry, and no State Store or Schema Registry traffic beyond the
  RPC affordances themselves.
- **One logical payload per direction** — opaque to the transfer layer, reassembled and
  only then deserialized into its declared type.
- **Strictly sequential, never interleaved.** The executor cannot begin responding until
  the full request is reassembled and deserialized, so there is no bidirectional
  interleaving to specify. This is the largest single simplification relative to
  streaming and it survives both scope changes.

### Prior art in this repo

[ADR 23 (branch `maxim/chunking`)](https://github.com/Azure/iot-operations-sdks/blob/maxim/chunking/doc/dev/adr/0023-large-message-chunking.md)
proposed chunking as a **transport-layer** concern: a `__chunk` user property, fixed-size fragments derived
from the negotiated maximum packet size, reassembly keyed by message id, SHA-256
integrity check, reassembly budget taken from the first chunk's message expiry
interval, and **mandatory support from a given protocol version onward with no
opt-out**.

### Hard constraints from the review

1. **No breaking change for existing peers.** DSS does not use the SDK; it
   re-implements an approximation of the mRPC wire protocol. Any change that makes a
   conforming-but-old peer fail is out. The room's conclusion: *"You add a new API side
   by side with the old API, and that's it."*
2. **Recompilation is acceptable**; wire-level breakage of deployed peers is not.
3. **One obvious API.** *"If I am writing an API that is semantically single request /
   single response, but I am unsure whether it will fit in one MQTT message — which API
   do I use?"* The answer must not be "it depends on the payload size at runtime".
4. **The application should not have to think about MQTT message sizes.** The SDK
   should reassemble into the declared, typed payload, the same way unary RPC does
   today.
5. **Broker limits are per-connection and per-deployment profile.** Chunk size must be
   computed at runtime from the negotiated value, not configured.

## Requirements for chunking

**Must:**

- Transfer a single logical payload larger than the connection's maximum packet size
  without the application splitting or reassembling it, in **both directions** — see
  [scope as it now stands](#scope-as-it-now-stands).
- Surface the reassembled payload as the **declared, deserialized type** — chunk
  boundaries are never visible to the application.
- Detect an incomplete or corrupted transfer **deterministically** and fail it with a
  distinct, catchable error. Never surface a partial payload as complete.
- Derive fragment size at runtime from the negotiated maximum packet size.
- Work under shared subscriptions — every fragment of one payload must reach the same
  executor.
- Bound receiver memory: a peer must not be able to force unbounded buffering.

**Must not:**

- Break a deployed peer that predates chunking.

**Explicitly not required** (per the review):

- Resuming a partially transferred payload after a peer restart. Restarting the whole
  operation is acceptable.
- Guaranteed ordering *on the wire* — reassembly is by index, so receive order is
  irrelevant as long as completeness is verifiable.

## The structural insight

Chunking's requirements are **a strict subset of streaming's, plus one addition**.

| Streaming requirement | Needed for chunking? |
| --- | --- |
| Producer may send its first entry without knowing the total | **No** — the payload is serialized first, so the total is always known up front |
| Standalone `last` control message to end a stream at an arbitrary point | **No** — `totalChunks` in the header makes the end self-evident |
| Bidirectional, interleaved, arbitrary relative timing | **No** — both directions may be chunked, but strictly in sequence: the request completes, then the response begins |
| Cancel at any time | Useful, not essential |
| Executor takeover mid-stream via shared subscription | Useful — but a takeover mid-payload means the transfer fails and restarts anyway |
| Per-entry user metadata and typed per-entry payloads | **No** — fragments are opaque byte ranges of one serialized payload |
| Exchange timeout carried live in the header | Yes, or an equivalent |
| Index + de-dup | Yes |
| — | **Plus: completeness verification and total-payload integrity, which streaming deliberately does not provide** |

Nearly all of ADR 25's wire complexity — the standalone `last`, the `d`/`c`/`s` tri-form
header, the tombstones, the bidirectional cancel handshake — exists to serve two
properties chunking does not have: **open-ended length** and **discrete, independently
typed entries**. This is the central tension: **building chunking on the streaming wire
buys reuse, but it inherits complexity chunking never needed, and it puts the whole
streaming protocol on the critical path for the P0 scenario.**

## Cross-cutting decisions

These must be answered under **every** option; they are not differentiators.

1. **Fragment size discovery.** In MQTT 5, `CONNACK`'s *Maximum Packet Size* is what
   the broker will accept from us; `CONNECT`'s is what we will accept from the broker.
   The publisher **does not know the subscriber's** limit, and per
   `[MQTT-3.1.2-25]` the broker **silently discards** a packet too large to deliver.
   So oversize payloads fail invisibly today, and our fragment size must be derived
   from the broker's `CONNACK` value minus worst-case overhead (topic, correlation
   data, all reserved user properties, MQTT fixed/variable headers), with a
   conservative safety margin. ADR 23 states this direction ambiguously and needs
   correcting.
2. **Where chunking sits relative to serialization.** Serialize → split; reassemble →
   deserialize. Fragments are opaque bytes and are *not* independently typed. This is
   the opposite of streaming, where each entry deserializes on its own.
3. **Integrity.** Index completeness plus total length already detects loss and
   truncation under QoS 1. A checksum only adds corruption detection. SHA-256 over
   50 MB on a constrained edge device is not free — evaluate CRC-32C, or make the
   checksum optional and length/index completeness mandatory.
4. **Failure semantics.** A missing fragment, an expiry lapse mid-reassembly, or a
   checksum mismatch ⇒ terminal, named error to the application (`ChunkTransferFailed`
   / `ChunkTimeout` / `ChunkIntegrityFailed`), buffers dropped, application re-requests.
5. **Memory bounds.** Maximum reassembled payload size, maximum concurrent
   reassemblies, and per-peer caps. Neither ADR 23 nor ADR 25 covers this and it is a
   denial-of-service surface: an unauthenticated-to-us peer can otherwise pin memory by
   opening reassemblies it never completes.
6. **Shared subscriptions.** `$partition` on every fragment so all fragments of one
   payload land on one executor, as both ADRs already require.
7. **Interaction with the unary executor's replay cache.** Today the executor caches
   responses by correlation to survive redelivery. A chunked request that is partly
   redelivered must not produce a second execution.
8. **Modeling and codegen.** WoT / DTDL must express that an affordance is chunked (or
   that chunking is always permitted), because codegen picks the envoy. This was the
   review's first structural objection and it is unresolved under every option.
9. ~~**Telemetry.**~~ **Resolved — out of scope.** Streaming as specified is strictly
   invoker/executor with a per-invoker response topic and has no telemetry story, so
   chunked telemetry would have eliminated any option built on the streaming envoys.
   Constraint 2 removes that argument, and it is the single reason Option B is still on
   the table. If telemetry ever returns to scope, revisit this first.

### The back-compat trap in "just add a new API side by side"

The review's conclusion — add a new API next to the old one — is not as cheap as it
sounds, and it is worth stating plainly before choosing:

- Two executors (unary and chunked) **cannot share one command topic**. Under a shared
  subscription the broker load-balances between them, so a request would reach whichever
  executor happened to be picked, including the one that cannot parse it.
- Therefore side-by-side means either **(a) a distinct topic per variant** — i.e. a
  distinct affordance in the model, `getSchema` and `getSchemaChunked`, with the
  version skew pushed onto the service owner — or **(b) one executor that inspects the
  header and dispatches**, which is capability negotiation by another name.
- **Response chunking can be made backward compatible**; request chunking cannot.
  An invoker can advertise "I can reassemble chunked responses" on its request, and an
  executor chunks only when that marker is present — old invokers keep working
  untouched. An invoker sending a chunked *request* has no way to know in advance
  whether the executor understands it, so a large request requires a distinct topic or a
  pre-established version contract.

Scenarios 1, 3 and 4 need only response chunking, so a response-direction capability
marker covers most of the surface. **Scenario 5 (`putSchema`) does not**, and it is the
reason a response-only mechanism cannot be the whole answer.

**The distinct topic is not purely a cost.** Because only chunk-capable executors
subscribe to the chunked topic, an invoker whose first request fragment comes back
`no matching subscribers` has learned the capability is absent — capability discovery for
the request direction, using the mechanism ADR 25 already specifies for
`NoAvailableStreamingExecutor`, broker-behaviour assumption included. Mixed old/new
executor fleets are fine; the chunked topic simply has fewer subscribers.

## Options

### Option A — Transparent transport-layer chunking (revive ADR 23)

Chunking lives in the MQTT session client, **below** the envoys. Any PUBLISH that
exceeds the negotiated limit is fragmented with a `__chunk` property and reassembled by
the receiving session client before the envoy ever sees it.

**Pros**

- One mechanism for **everything**: unary RPC, telemetry, streaming, State Store,
  Schema Registry, connectors — though with telemetry and the non-RPC clients out of
  scope, most of that breadth is now worth less than it looks.
- **Zero new public API.** Answers constraint 3 perfectly — there is nothing to choose,
  because the existing API always works.
- Typed serialization is untouched; the envoy still deserializes one declared type.
- No WoT/codegen change: chunking is not an interaction pattern, it is a transport
  property.
- Smallest possible per-fragment overhead.

**Cons**

- **Breaking on the wire**, and ADR 23 makes it mandatory at a protocol version with no
  negotiation. A non-chunk-aware peer (DSS, hand-rolled clients) receives fragments as
  separate messages and fails. This collides head-on with hard constraint 1.
- Reassembly buffers sit at the transport layer with no application context — hardest
  place to apply sensible memory bounds, and the easiest to attack.
- No progress signal, no cancellation, no backpressure: the receiver cannot say "stop",
  and the sender cannot learn that reassembly failed until a timeout elapses.
- Duplicates streaming's de-dup, expiry and buffering machinery in a second place.
- ~~Does not serve scenario 2 (discrete entries) at all.~~ Moot — scenario 2 is out of
  scope.
- Interacts subtly with the executor's replay cache and with `$partition`, both of which
  are protocol-layer concepts leaking into the transport.

**Cost:** moderate implementation, high compatibility risk.

### Option B — Chunking as a profile on the streaming wire protocol

Keep `__stream` exactly as ADR 25 specifies. Add a **chunked** envoy pair
(`ChunkedCommandInvoker` / `ChunkedCommandExecutor`) whose stream entries are opaque
byte ranges of one serialized payload, with total length and optional checksum carried
in the existing per-stream metadata. The consumer reassembles, verifies completeness by
index, then deserializes into the declared type. Any gap ⇒ terminal named error.

Requires one framing change to ADR 25: the "chunking is a non-requirement / entries are
losable" language becomes "the streaming layer surfaces index, total and terminal
signals so a strict layer can be built on top".

**Pros**

- **One wire protocol** to specify, version, METL-test and maintain.
- Reuses correlation, the live timeout countdown, cancellation, `$partition`, executor
  takeover, tombstones and index de-dup — all already specified.
- Manual acknowledgement gives real backpressure, which is the answer to the review's
  *broker receive maximum* concern about an executor that buffers a whole request before
  responding.
- Cancellation gives exactly the behaviour the review asked for: detect a stale or
  orphaned transfer, cancel it, let the invoker restart.
- Leaves the door open for true streaming later with no second protocol.

**Cons**

- **Streaming's full wire is now on the critical path for the P0 scenario.** We cannot
  ship chunking without shipping and stabilising a protocol that was just judged to be
  more complex than the problem requires.
- Inherits complexity chunking does not need (standalone `last`, tri-form header,
  bidirectional cancel handshake, per-entry HLC timestamps).
- Per-fragment header overhead on every fragment of a 50 MB payload.
- ~~**No telemetry story** — streaming is invoker/executor only.~~ No longer
  disqualifying now that telemetry is out of scope; this was previously B's fatal flaw.
- Still a new API side by side, so the back-compat trap above applies in full.

**Cost:** low incremental cost *given* streaming ships; high total cost if streaming
would otherwise be deferred.

### Option C — Chunking as its own envoy pair and its own wire protocol

A dedicated `__chunk` wire (essentially ADR 23's header) exposed through dedicated
chunked envoys — not the transport layer, and not the streaming wire. Implementation
shares internal machinery (index de-dup, expiry accounting, buffer management) with
streaming where convenient, but the two protocols are independent on the wire.

**Pros**

- The wire can be **minimal and strictly verifiable**: `totalChunks` is always known up
  front, so there is no `last`, no control form, no status form, no tombstone — a
  fragment either arrives or the transfer fails.
- **Does not depend on shipping streaming**, so the P0 scenario can land first.
- **Direction-agnostic** — one header serves request and response fragments, so
  `putSchema` needs no second format. Extends to telemetry unchanged if that ever
  returns to scope.
- The **distinct affordance doubles as capability discovery** for the request direction
  (see the back-compat trap above) — a cost that turns out to pay for itself.
- Easiest to bound and reason about for memory safety, and the limits sit where the
  executor can enforce them.

**Cons**

- **Two multi-message protocols on the wire** to version, test and document, with a
  visible conceptual overlap — exactly the duplication Carter warned would "pollute the
  implementation".
- Cancellation, progress and backpressure have to be re-specified or forgone.
- Once the request direction is chunked, C's wire needs `$partition`, an in-band
  timeout, index de-dup and two-sided buffering — **a meaningful fraction of `__stream`**.
  The overlap with Option B is larger than it first appears.
- If streaming ships later, we must explain to users when to use which.

**Cost:** low-to-moderate implementation, low compatibility risk, ongoing maintenance
cost of a second protocol.

### Option D — Re-implement unary RPC on top of streaming and hide it

Carter's "platonic ideal": keep the public API as `InvokeAsync(request) -> response`,
implement it over the streaming wire, chunk automatically when the payload does not
fit, and never surface streaming at all.

**Pros**

- The single best answer to constraints 3 and 4 — one API forever, and the application
  genuinely never thinks about message sizes.
- One code path, one protocol, one set of tests.
- No new modelling concepts for the common case.

**Cons**

- **Hard breaking wire change for every existing mRPC peer**, with no opt-out path.
  Directly violates hard constraint 1 and would break DSS.
- Behavioural differences leak through the abstraction: streaming deliberately keeps
  **no executor replay cache**, while unary RPC depends on one for idempotency; timeout
  and acknowledgement semantics differ; executor-takeover semantics differ.
- Largest blast radius of any option, applied to an already-shipped protocol.
- Viability is unproven — the review explicitly declined to commit to it
  (*"I don't want to sell anybody a dream"*).

**Cost:** very high. Listed for completeness and to be rejected explicitly rather than
by omission.

### Option E — Capability-negotiated chunking on the existing mRPC wire

Leave the unary mRPC protocol and topics as they are. Add an invoker-advertised
capability marker on the request (for example a reserved user property carrying the
maximum payload the invoker can reassemble). An executor chunks its **response** only
when the marker is present; otherwise it behaves exactly as today. Chunked **requests**
require a distinct topic or an established version contract, per the back-compat trap
above.

**Pros**

- **Truly backward compatible** for the response direction, which covers scenarios 1, 3
  and 4. Old invokers are untouched; new invokers get large responses.
- No new envoy, no new API to choose, no modelling change for the response case.
- Incremental rollout — a service can enable it before all clients update.
- Smallest possible step toward the P0 scenario.

**Cons**

- **Asymmetric**: does not solve large requests (scenario 5, `putSchema`) without a
  second mechanism.
- Bolts a second framing onto the unary protocol, complicating a wire we otherwise want
  to leave alone.
- Feature negotiation is precisely what ADR 23's footnote rejected as added complexity —
  that decision needs to be revisited explicitly, not silently reversed.
- Still needs all cross-cutting decisions (buffers, integrity, expiry) solved.

**Cost:** low implementation, low compatibility risk, limited coverage.

### Option F — Out-of-band transfer ("claim check")

Do not put large payloads on MQTT at all. The response carries a reference; the bytes
move over a separate channel (blob store, State Store, HTTP endpoint).

**Pros**

- No protocol change; no broker memory pressure; no fragment reassembly; arbitrary
  payload sizes. This is what the review implicitly endorsed for video —
  *"sending video streams through MQTT broker… is going to choke the broker"*.

**Cons**

- Requires an available store at the edge, plus lifecycle, authorization and cleanup for
  the referenced objects.
- Two failure domains, two authorization models.
- Does not satisfy the stated requirement, which is to move the bytes over MQTT.

**Cost:** high infrastructure cost, out of scope as a primary answer — but worth
recording as the escape hatch for genuinely large media.

### Option G — Do nothing at protocol level

Publish a documented application-level pattern and let each team implement it.

**Cons:** this is the status quo the ADRs were written to end — *"every team
differently, none of it recoverable after a crash"*. Listed only as the baseline to
measure against.

## Comparison

| | A. Transport | B. On streaming | C. Own protocol | D. RPC over streaming | E. Negotiated | F. Out-of-band |
| --- | --- | --- | --- | --- | --- | --- |
| Backward compatible on the wire | ✗ | new API | new API | ✗✗ | **✓** | ✓ |
| Ships without streaming | ✓ | **✗** | ✓ | ✗ | ✓ | ✓ |
| Covers large **responses** (scenarios 1, 3, 4, 5) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Covers large **requests** (scenario 5) | ✓ | ✓ | ✓ | ✓ | **✗** | ✓ |
| Covers **telemetry** *(out of scope)* | ✓ | ✗ | ✓ | ✗ | ✗ | ✓ |
| Covers scenario 2, discrete entries *(out of scope)* | ✗ | ✓ | partial | ✓ | ✗ | ✗ |
| Application sees typed payload | ✓ | ✓ | ✓ | ✓ | ✓ | ✗ |
| Cancellation / backpressure | ✗ | **✓** | to design | ✓ | ✗ | n/a |
| Number of wire protocols to maintain | 1 (+streaming) | **1** | 2 | 1 | 1 (+ext) | 1 |
| Memory-safety story | weakest | good | **best** | good | good | n/a |
| Blast radius | high | medium | low | **highest** | **lowest** | low |

The two rows marked *out of scope* are kept for the record but no longer influence the
decision — they are what previously eliminated Option B. The **large requests** row does
influence it: scenario 5 is what demotes Option E.

## How the scope changes re-score the options

[Scope as it now stands](#scope-as-it-now-stands) is stated up front. This section
records what the two shifts did to the evaluation above.

### What removing telemetry and scenario 2 takes away

- **No telemetry** ⇒ the structural argument that eliminated Option B is gone.
- **No scenario 2** ⇒ nothing in scope needs streaming's defining capabilities, so
  streaming no longer "ships anyway". Option B must justify building the whole `__stream`
  wire on chunking's budget alone.

### What restoring large requests brings back

| Returns | Why it matters |
| --- | --- |
| `$partition` on every request fragment | All fragments of one request must reach the same executor under a shared subscription. Cross-cutting decision 6 is live again. |
| **Executor-side** memory exposure | Response-only chunking meant the *invoker* buffered a payload it asked for and sized. With request chunking the *executor* buffers whatever peers push at it — a far more serious denial-of-service surface, and the reason an advertised limit should be a **size**, not a boolean. |
| Orphaned partial reassembly at the executor | An invoker that dies mid-request leaves a buffer behind; needs an expiry and cleanup path. |
| Replay-cache interaction | A partially redelivered chunked request must not produce a second execution. |
| Receive Maximum / in-flight window | A burst of request fragments interacts with the QoS 1 in-flight limit — the *broker receive maximum* concern raised in the review. |
| **Direction-agnostic header** | One `__chunk` format must serve requests and responses. |
| **In-band capability negotiation stops being sufficient** | The invoker must choose a wire format before hearing anything from the executor, which is what hands Option C its discovery mechanism and demotes Option E. |

### What it does to each option

- **A — unchanged by the clarification, still weakened overall.** It covers both
  directions for free, so the amendment costs it nothing. But constraint 2 already
  removed most of its distinguishing value (telemetry and the service clients), and the
  wire break it cannot avoid is unchanged.
- **B — back in genuine contention.** Fragmented request *and* response on one
  correlation is closer to streaming's shape than response-only was, and C's wire now
  needs `$partition`, an in-band timeout, index de-dup and executor-side buffering —
  much of what `__stream` already specifies. B still carries real excess (`last`, the
  control and status forms, tombstones, the cancel handshake, interleaving, per-entry
  types and metadata) and still requires the entire streaming protocol to ship first.
  The capability gap narrowed; the sequencing cost did not.
- **C — front-runner again.** One direction-agnostic header serves both directions, the
  wire stays minimal because totals are known up front, and the distinct affordance
  doubles as capability discovery.
- **E — demoted to a partial answer.** It covers responses cleanly and cannot cover
  requests: chunking a request on the existing topic means an old executor receives
  fragment 0 as a whole request and fails to deserialize it. Using that failure as a
  capability probe would be negotiation-by-error-code — fragile, and it pollutes error
  semantics. E remains worth having as a cheap early step for `getSchema`, not as the
  answer.

### A defined error instead of a silent discard

When an **old** invoker requests a response that does not fit, the broker today silently
discards it (`[MQTT-3.1.2-25]`) and the caller sees a timeout. A chunk-capable executor
can instead return a defined `PayloadTooLarge` status — a strict improvement, and
readable by old clients since it is an ordinary mRPC status code.

## Recommendation (revised)

1. **Adopt Option C.** A dedicated `__chunk` wire, direction-agnostic, exposed through
   chunked envoys on a distinct affordance. It is the only option that covers both
   directions without a wire break, its wire stays minimal because totals are always
   known before the first fragment ships, and the distinct affordance it requires is
   also how request-direction capability discovery works.
2. **Keep Option E as an optional early step** for response-only operations such as
   `getSchema`, if we want something shippable before the chunked envoys land. It must
   not be presented as the destination.
3. **Return a defined `PayloadTooLarge` status** to invokers that cannot reassemble,
   replacing today's silent discard.
4. **Decouple ADR 25.** With scenario 2 out, streaming has no P0 scenario left and must
   not sit on the chunking critical path. Keep it as designed, mark it future work, ship
   it on its own schedule.
5. **Reject Options A and D explicitly** — A for the wire break it cannot avoid, D on
   hard constraint 1.
6. **Record Option F** as the escape hatch for media-scale payloads.

The live argument is **C versus B**, and it is now a genuine one: C's wire has grown to
include `$partition`, in-band timeout, index de-dup and two-sided buffering, which is a
meaningful fraction of `__stream`. The question to settle is whether we will ever ship
streaming. If yes, B avoids a permanent second protocol. If no — and nothing in scope
currently requires it — B means building `last`, tombstones and a bidirectional cancel
handshake to serve a request/response operation that needs none of them.

## Open questions for the review

Answered by the scope work — confirm rather than debate:

1. ~~Is chunked telemetry in scope?~~ **No.**
2. ~~Is scenario 2 chunking or streaming?~~ **Neither — out of scope; the application
   hands us a collection and we split it by size.**
3. ~~Does the header need to be direction-agnostic?~~ **Yes** — `putSchema` chunks both
   directions.

Still open:

4. **Will we ever ship streaming?** *(Decides C vs B — the pivotal question now.)*
5. What does a peer advertise, and how is it enforced — a **maximum reassembled size**
   rather than a boolean, so the executor can reject before buffering and the invoker
   can fail fast? Where does the executor publish its limit, given a request arrives
   before any executor reply?
6. How does a chunked affordance appear in the **WoT Thing Model**, and what does codegen
   emit — a separate affordance, or a flag that produces a second topic?
7. **Executor-side memory limits**: maximum reassembled payload, maximum concurrent
   reassemblies, per-peer caps, and the behaviour when they are hit. This is now the
   primary denial-of-service surface.
8. Is a **checksum** required, or is index completeness plus total length sufficient
   under QoS 1? If required, is SHA-256 affordable on the target devices?
9. How does chunking interact with the **executor's replay cache**? Cache the assembled
   response, or the fragment set?
10. Do **SDK-shipped clients** (Schema Registry especially — scenarios 4 and 5 are
    literally `getSchema`/`putSchema`) use the chunked affordance by default?
11. Confirm the failure model in writing: **any incomplete transfer fails the whole
    operation with a distinct error, and the application re-requests.** No partial
    payload is ever surfaced as complete.
