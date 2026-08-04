---
marp: true
theme: default
paginate: true
size: 16:9
header: 'ADR 25 — RPC Streaming'
footer: 'doc/dev/adr/0025-rpc-streaming.md'
style: |
  section { font-size: 25px; }
  section.lead { text-align: center; }
  section.lead h1 { font-size: 52px; }
  h1 { font-size: 38px; }
  h2 { font-size: 30px; }
  pre { font-size: 16px; line-height: 1.35; }
  table { font-size: 21px; }
  blockquote { font-size: 21px; color: #555; }
  section.divider { text-align: center; }
  section.divider h1 { font-size: 46px; }
---

<!-- _class: lead -->
<!-- _paginate: false -->
<!-- _header: '' -->

# ADR 25 — RPC Streaming

Many requests and many responses per command invocation, over MQTT 5

**60 min:** ~30 min walkthrough · ~30 min discussion

Source of truth: `doc/dev/adr/0025-rpc-streaming.md`
Visual companion: `0025-rpc-streaming-lifecycle-diagrams.md`

<!--
Opening: this ADR has been through a couple of focused reviews, but most of the room
has not read it. So today is a from-scratch walkthrough, not a diff review.
Set the contract up front: I will talk for about 30 minutes, please hold structural
questions to the marked pause points, and we keep the back half for discussion.
-->

---

## Agenda and what I need from you

| # | Segment | Time |
| --- | --- | --- |
| 1 | Problem, requirements, prior art | ~6 min |
| 2 | The model: exchange, two half-streams, completion | ~6 min |
| 3 | Happy paths on the wire | ~7 min |
| 4 | `__stream` property and routing | ~8 min |
| 5 | Failure semantics (timeout, cancel, errors, recovery) | ~3 min |
| — | **Discussion / decisions** | ~30 min |

**Asks:** (1) agree this is a *separate* communication pattern; (2) sign off on the
`__stream` wire shape before implementation; (3) confirm the accepted limitations.

<!--
Say explicitly: the wire shape is the expensive thing to change later, because it
becomes protocol version 1.0. Everything else is refactorable.
-->

---

<!-- _class: divider -->

# 1. Problem, requirements, prior art

---

## Today: one request, one response

Our command pattern is strictly unary — invoker sends one request, executor returns one response.

Customers have asked for the shapes that do not fit:

- upload/ingest: **many requests**, one summary response
- long-running query or subscription-like read: one request, **many responses**
- interactive session: **many of both**, interleaved, either side able to stop

Workarounds today mean inventing per-application correlation, chunking, and cleanup on top of
unary RPC — every team differently, none of it recoverable after a crash.

<!--
Keep this short. The point is only: the gap is real and recurring, and the workarounds
are the part that costs us, because each is a private protocol we do not test.
-->

---

## Requirements (the MUSTs)

- Arbitrary number of requests **and** responses per invocation; a stream may be exactly one entry
- Producer may send its first entry **without knowing the total count**
- Each surfaced entry carries its **index** in the stream
- Multiple streaming commands run **simultaneously and independently**
- **Either** side may cancel at any time
- Entries flow at **arbitrary times relative to each other** — only rule: the invoker starts with a request
- Either side may **end its own stream gracefully** at any point, without a full final entry
- If an executor crashes, another executor in the group (or the same one restarted) can **pick the request stream up**

<!--
Highlight two: "without knowing the total count" kills any header-declared length,
and "end gracefully without a full entry" is what forces the standalone last message.
The executor-crash requirement is what forces shared subscriptions and partitioning.
-->

---

## Non-requirements (deliberately out of scope)

| Not doing | Why |
| --- | --- |
| Different payload shapes per entry | One stream, one type — keeps codegen and validation simple |
| Chunking one large payload across entries | We do not verify a stream arrived in full — a stricter layer on top does that (ADR 33) |
| Delivering entries in their **intended** order | Entries may be lost; we surface in receive order and expose index + timestamp |
| Resuming after the **invoker** crashes | Its response topic is unique to it — in-flight responses cannot be re-routed |

These are the four things people ask for first. They are accepted limitations, not oversights.

<!--
Pre-empt the loudest questions. Ordering is the one to dwell on: we hand the app the
index, the app decides whether it cares. Guaranteed ordering over a lossy transport
would require gap detection plus retransmission, which is a much bigger protocol.
-->

---

<!-- _class: divider -->

# 2. The model

---

## Decision at a glance

| Concern | Decision |
| --- | --- |
| Pattern | **New** pattern: streaming command invoker / executor, own protocol version `1.0` |
| Unit of work | One **exchange** = one correlation GUID = two streams |
| Wire marker | One `__stream` user property, three tagged forms: `d` data, `c` control, `s` status |
| End of a stream | Standalone **`last`** control message (`c:<idx>:last`) |
| Completion | Exchange completes only when **both** streams have closed |
| Timeout | Single **exchange budget**, local timers on both sides, remaining value on every request |
| Cancellation | `Canceled` status, `__stat` `499` — one message both initiates and confirms |
| Errors | Protocol violations only; application errors are ordinary data entries |
| Routing | Shared-subscription command topic + `$partition` = invoker client id |

<!--
This is the slide to photograph. Everything after it is elaboration; if someone drops
off, this is the summary they need.
-->

---

## One exchange, two streams

- Each side **produces** one stream and **consumes** the other:
  invoker produces requests / consumes responses; executor is the mirror
- Both together are one **exchange** — the unit of correlation, timeout, and cancellation
- Streams surface as plain async sequences: you **supply** the one you produce, **iterate** the one you consume
- Completion, cancellation, and timeout are **exchange-scoped** — one per invocation, never per direction

```text
             requests  (d:0:T, d:1:T, ..., c:n:last:T)
   Invoker  ───────────────────────────────────────────▶  Executor
            ◀───────────────────────────────────────────
             responses (d:0,   d:1,   ..., c:m:last)
```

<!--
The asymmetry to stress: the two directions are independent in progress but joined in
lifecycle. One cancel token, one timeout, two independent close events.
-->

---

## Entries and the two metadata scopes

Every entry is a user **payload** plus metadata, and metadata lives at two scopes:

| Scope | Applies to | On the wire |
| --- | --- | --- |
| **Message** metadata | the single entry it travels with | that entry's user properties |
| **Stream** metadata | the whole stream in one direction | repeated on every message, read once |

- Stream metadata is **asymmetric**: request-stream metadata ≠ response-stream metadata
  (mirrors unary RPC's request/response metadata)
- It repeats on every message so that **losing the first message does not lose it** — which is also
  what lets a replacement executor recover mid-stream

<!--
"Repeat and read once" is the recurring trick in this design: anything a late joiner or
a replacement needs must be on every message, because there is no first-message guarantee.
-->

---

## Completion: both halves must close

- A producer ends **its own** stream with `last`
- Closing one stream does **not** end the exchange — the side stays active for control traffic
- **Gracefully complete** = invoker sent its `last` request **and** received the `last` response
  (and symmetrically for the executor)
- Any other terminal — cancel, timeout, protocol violation — ends the whole exchange immediately
- After terminal, state is kept as a **tombstone** so late or duplicate packets stay routable
  and are never mistaken for a new stream

```text
BothOpen ──my last──▶ ProducedClosed ──peer last──▶ Completed
BothOpen ──peer last──▶ ConsumedClosed ──my last──▶ Completed
Active   ──cancel | timeout | protocol error──────▶ Terminal
```

<!--
This was the one real correctness bug found in review: an earlier draft let the response
last alone end the exchange, which left the request half unprotected. Both-halves is the
fix, and the state machine is identical for both roles.
-->

---

<!-- _class: divider -->

# 3. Happy paths on the wire

---

## Happy path 1 — establishment, and why we return early

```text
 1  INV   SUBSCRIBE  clients/<invoker-id>/...        response topic, before publishing
 2  INV   PUBLISH    <command topic>   corr=G   __stream=d:0:30   $partition=<invoker-id>
 3  ===>  InvokeStreamingCommandAsync RETURNS (responseStream, exchangeHandle)
 4  EXE   receives request[0]  ->  OnStreamingCommandReceived(requests, meta, exchange)
 5  EXE   PUBLISH    <response topic>  corr=G   __stream=d:0
 6  INV   buffers response[0] until the app starts iterating
```

- The invoker activates response reception, sends the **mandatory first request**, then returns —
  it does **not** wait for the second request or for the request stream to end
- Otherwise a full-duplex app deadlocks: request *n+1* depends on response *n*, which needs the call to return
- Empty request stream or setup failure → the invocation **fails before** an exchange exists

<!--
This ordering was validated against a formal model: making the return wait for the
request stream to end produces a genuine liveness failure. Mention it as evidence, not
as a tangent.
-->

---

## Happy path 2 — full duplex, interleaved

```text
INV ──▶ cmd    d:0:30    request[0]      $partition=P
EXE ──▶ resp   d:0       response[0]
INV ──▶ cmd    d:1:28    request[1]      $partition=P
EXE ──▶ resp   d:1       response[1]
EXE ──▶ resp   d:2       response[2]     executor may run ahead — no lock-step
INV ──▶ cmd    d:2:25    request[2]      $partition=P
```

- Indexes are **per producer**, so the two directions count independently
- Consumer de-dups on **correlationId + index + timestamp** (HLC) — QoS 1 may redeliver
- Entries are acknowledged automatically on delivery; **manual ack is executor-only**
  (the invoker cannot resume a response stream after a crash, so holding an ack buys it nothing)

<!--
Two things to call out: the timeout field is a live countdown (30, 28, 25), and the
timestamp in the de-dup key is what separates a redelivery from a restarted producer
that reused index 0. Come back to that on the recovery slide.
-->

---

## Happy path 3 — graceful close

```text
INV ──▶ cmd    c:3:last:22    no payload, no app user properties, $partition=P
EXE                            signals "request stream ended" to the app
EXE ──▶ resp   c:3:last       no payload
INV                            signals "response stream ended" to the app
                               both closed  ->  Completed  ->  tombstone retained
```

- `last` shares the producer's index counter with data — it is the next index, not a flag on an entry
- Receiving data for a stream **after** its `last` is a protocol violation (delivery order is guaranteed)
- Control traffic keeps flowing after `last` until the exchange is terminal

<!--
Pause point 1. Ask for questions on the model and the happy paths before going into
the wire format, because the rest only makes sense on top of these three traces.
-->

---

<!-- _class: divider -->

# 4. `__stream` and routing

---

## Topics and routing

| Direction | Topic | Subscription | Notes |
| --- | --- | --- | --- |
| Invoker → executor | **command topic** | **shared** subscription | same prefix/suffix and custom topic tokens as unary RPC |
| Executor → invoker | **response topic** `clients/{invoker id}/...` | invoker only, not shared | invoker subscribes **before** publishing |

- One **correlation GUID** identifies the whole exchange; every message of both directions carries it
- Each direction carries its own **data, control, and status** messages on the same topic
- Every request-direction message also carries the **response topic** and
  `$partition = <invoker client id>`

<!--
Shared subscription is what satisfies the executor-crash requirement: the group survives
an individual executor. It is also what creates the partitioning requirement on the next
slide.
-->

---

## `$partition` — on *every* command-topic packet

Shared subscription means the broker is free to hand any packet to any executor in the group.

**Every** command-topic packet for an exchange must carry the same `$partition`:

- request data (`d:…`)
- the `last` request (`c:…:last`)
- the invoker's `Canceled` status (`s:…` `__stat=499`) — whether initiating or confirming

Otherwise a packet lands on an executor that holds **no state for that correlation** and is
silently dropped — a lost `last` or a lost cancel confirmation, with no error anywhere.

Response-topic packets need only the correlation, since that topic is already unique to the invoker.

<!--
This is a review finding, not an original design point: the first draft only partitioned
request data. It was proven with a two-executor routing model where the last request
routed to the wrong executor in four steps.
-->

---

## The `__stream` user property

Every streaming PUBLISH carries exactly one `__stream` value, in one of three **tagged** forms:

```txt
<property_value> ::= <stream_entity_metadata> | <stream_control_metadata> | <stream_status_metadata>

<stream_entity_metadata>  ::= "d" ":" <message_index> [ ":" <timeout_length> ]
<stream_control_metadata> ::= "c" ":" <message_index> ":" <control_command_word> [ ":" <timeout_length> ]
<stream_status_metadata>  ::= "s" ":" <message_index> [ ":" <timeout_length> ]

<message_index>         ::= <uint>
<timeout_length>        ::= <uint>
<control_command_word>  ::= "last"
```

Because the form is **tagged**, a message only ever carries the fields that apply to it —
there are no fields to ignore, and no ambiguous empty positions.

<!--
Emphasise the tag choice: an earlier shape was a fixed positional tuple with empty slots,
which forced every reader to know which slots were meaningful for which message kind.
-->

---

## `__stream` fields

| Field | Type | Meaning |
| --- | --- | --- |
| `message_index` | uint | **d / c**: position in the **producer's** stream — data and control share one counter. **s**: index of the **received** message the status is about (the *peer's* counter). |
| `control_command_word` | `last` | **c** only — the standalone final message that closes the producer's stream. No payload, no app user properties. |
| `timeout_length` | uint | Invoker's **remaining exchange budget in seconds**. **Request direction only**; omitted executor → invoker. |

Companion property on status messages: `__stat` (`499` = `Canceled`, or a `4xx`/`5xx` protocol error),
plus optional human-readable `__stMsg`.

<!--
The index switching reference frame on status messages is the subtle bit. Say it plainly:
d and c count my own stream, s points at your message.
-->

---

## `__stream` by example

**Request direction** (invoker → executor) — timeout present, `$partition` required:

| Value | Meaning |
| --- | --- |
| `d:0:10` | data entry 0; 10 s of exchange budget left |
| `c:4:last:6` | request stream's final message at index 4; budget down to 6 s |
| `s:1:10` | status about the **received response 1** — `__stat` carries the code |
| `s:0:10` + `__stat=499` | cancel the whole exchange (index 0 is meaningless here) |

**Response direction** (executor → invoker) — identical forms, timeout omitted:
`d:0` · `c:7:last` · `s:3` · `s:0`

<!--
Walk one request-direction and one response-direction value out loud, character by
character. This is the slide people will reference during implementation.
-->

---

## Why `last` is its own message

Three options were considered:

| Option | Verdict |
| --- | --- |
| Final-message flag on a **fully-fledged entry** | Fails the requirement to end a stream when no entry is available to send |
| Flag allowed on **either** a full entry or a bare message | Ambiguous when the app sends no payload and no user properties — cannot tell "stream over" from "final entry" |
| **Standalone `last` control message** | Unambiguous, always available, costs one extra small publish per stream |

Consequence: `last` carries no payload and no application-provided user properties, and is
**never surfaced as a stream entry** — it is delivered as a "stream ended" signal.

<!--
Cheap slide, but it closes off a question that came up in every previous review.
-->

---

<!-- _class: divider -->

# 5. Failure semantics

---

## Timeout: one budget for the exchange

- Invoker configures an **exchange timeout** — total elapsed budget, **not** an inactivity timer,
  and it never resets. Configurable default; positive, finite, rounded up to whole seconds
- Both sides run **local** countdowns: invoker from sending request 0, executor from receiving it
- On expiry a side reports the timeout to its own app and stops — **no timeout status is ever sent**;
  the peer reaches its own timeout independently
- Every request-direction message repeats the **remaining** budget, so a replacement executor
  inherits the true time left

**Per-message expiry**: defaults to the remaining budget, always **capped** at it, must be positive and
finite — a message can never outlive its exchange. The receiver reuses it as the de-dup cache lifetime.

<!--
Rejected alternative worth naming: an idle timeout with heartbeats. It supports unbounded
streams but adds heartbeat traffic and a second, clashing timeout semantic. Deferred until
a customer actually needs an unbounded stream — flag it as an open discussion item.
-->

---

## Cancellation: `499`, and one message does both jobs

- Either side may cancel while the exchange is active; cancellation is **exchange-scoped**
- Sent as a status message: `__stream=s:0` (+ timeout on the request direction), `__stat=499`
- **The initiating cancel and its confirmation are the same message** — so receiving `Canceled`
  always means the exchange is over, no matter who started it
- Idempotent: re-issue freely; a terminal tombstone **re-answers** a repeated cancel
- After sending, a side keeps delivering **in-flight** entries to its app until the peer's
  `Canceled` arrives or the exchange times out

Invoker cancels on the command topic (with `$partition`); executor cancels on the response topic.

<!--
The symmetry is the point: one message shape, no separate ack type, no "who cancelled
first" race to resolve. Re-answering from the tombstone is what makes a lost confirmation
recoverable.
-->

---

## Errors: protocol violations only

A **protocol violation** is a correlation-matched message that breaks the wire contract:

- payload cannot be deserialized to the stream's type
- missing or malformed `__stream`
- incompatible streaming protocol version
- published at **QoS 0**
- **sequencing break** — data after `last`, or a request `last` with no preceding entry

Any of these is **terminal**: recipient returns `s:<index>` + `__stat` (index is diagnostic only),
both sides end the exchange. Unmatched or junk data is acknowledged and discarded — it never kills a stream.

**Application errors are out of scope.** `__apErr` is never set on a streaming message and is ignored
if present; an app signals its own errors as ordinary data entries. Success is implicit — no status means accepted.

<!--
Expect pushback on application errors. The argument: an application error is a value your
schema should express, and putting it in the transport forces every language SDK to model
a second error channel that codegen cannot type.
-->

---

## Disconnection and recovery

- Inherits session-client semantics: **if the session survives**, queued publishes flush and
  unacknowledged inbound messages redeliver (QoS 1, de-duplicated). If the session is lost, that
  side's stream state is gone and the peer falls back to its timeout
- A queued message keeps only its **remaining** expiry — it may lapse before reconnection.
  That entry is lost; entries are self-contained, so this is tolerated
- **Executor crash is survivable**: shared subscription hands the exchange to another executor, which
  recovers from the correlation, the repeated stream metadata, and the remaining budget on each request
- A replacement executor restarts response indexes at **0** — hence de-dup on index **and** HLC
  timestamp: a redelivery repeats both, a restart reuses an index with a **new** timestamp
- **Invoker crash is not survivable** — its response topic is unique to it, there is no load balancing
- `no matching subscribers` PUBACK → surfaced as **`NoAvailableStreamingExecutor`**, exchange ends
  (broker-specific assumption, guarded by end-to-end tests)

<!--
Two accepted limitations on this slide: no invoker recovery, and reliance on broker
behaviour for no-matching-subscribers. Both should be explicitly acknowledged in
discussion rather than discovered later.
-->

---

## What it looks like to a user (.NET sketch)

```csharp
// Invoker: supply the request stream, get back the response stream + exchange handle.
var (responses, exchange) = await invoker.InvokeStreamingCommandAsync(
    requests,                       // IAsyncEnumerable<StreamingExtendedRequest<TReq>>
    streamMetadata,                 // RequestStreamMetadata
    exchangeTimeout: TimeSpan.FromSeconds(30));

await foreach (var r in responses.Entries) { /* r.Payload, r.Metadata.Index, .Timestamp */ }
await exchange.Completion;          // faults or cancels on any non-graceful terminal

// Executor: request stream in, response stream out.
executor.OnStreamingCommandReceived = (requests, requestMeta, exchange) => (responses, responseMeta);
executor.AutomaticallyAcknowledgeRequests = true;   // false -> ack each entry yourself
```

Illustrative only — Rust and Go will expose the equivalent shapes idiomatically.

<!--
Keep this brief; the API is the least expensive part to change. The one durable point is
that both roles are symmetric: supply one sequence, iterate the other, one handle for
lifecycle.
-->

---

## Where we are and what I need

**Status:** ADR drafted and reviewed in depth by a small group; wire shape, completion, routing, and
cancellation stabilised. Protocol version starts at **1.0**, independent of unary RPC.

Decisions I want from this room:

1. **Separate pattern** — agreed, or push to fold into unary RPC?
2. **`__stream` shape** — sign off, or last objections to the tagged-form grammar?
3. **Accepted limitations** — no invoker recovery, no ordering guarantee, no in-pattern chunking
   (deferred to ADR 33), no application-error channel
4. **Timeout model** — total exchange budget now; idle + heartbeat deferred. Does anyone have a
   customer scenario that needs an unbounded stream?

<!--
Land the plane here, then open the floor. If the room only settles items 1 and 2 today,
that is enough to start implementation.
-->

---

<!-- _class: divider -->

# Appendix

Backup slides for discussion

---

## Anticipated questions (1/2)

**"Why not guarantee ordering?"** Any entry can be lost to expiry; guaranteeing order needs gap
detection plus retransmission — a much larger protocol. We surface index + timestamp so the app decides.

**"Why no response cache like unary RPC?"** A response stream may be unbounded; replaying it is not
feasible. This is a core reason the patterns are separate.

**"What if two executors both take the exchange?"** They cannot — `$partition` pins every
command-topic packet of an exchange to one executor in the shared-subscription group.

**"Why does the timeout only travel one way?"** The invoker owns the budget. The executor derives its
own from the first request it sees, so a replacement executor gets the true remaining time.

---

## Anticipated questions (2/2)

**"Why is manual ack executor-only?"** Manual ack exists so an unprocessed entry is redelivered after
a crash. The invoker cannot resume a response stream after a crash, so it would buy nothing.

**"Does `last` mean the exchange is over?"** No — it closes **one** stream. Control still flows; the
exchange ends only when both close, or on cancel / timeout / protocol violation.

**"What if the peer never sends `last`?"** The exchange timeout is the backstop; each side terminates
locally without needing the peer.

**"Can we add idle timeout later?"** Possible, but two timeout semantics would clash. Better to decide
now if there is a real unbounded-stream scenario.

---

## Rejected alternatives — quick reference

| Alternative | Why not |
| --- | --- |
| Executor decides per-invocation whether to stream | Forces the streaming API on every caller; awkward for the 1:1 case |
| Same protocol as unary RPC | Unrepresentable states, optional-handler problem, cache mismatch |
| Idle timeout kept alive by heartbeats | Heartbeat traffic + per-message resets + extra failure modes; revisit only for a real unbounded stream |
| Account for broker-side time so both sides expire together | Extra metadata for an uncommon case |
| gRPC-style "time from final request to final response" | Assumes a live connection; blind to a crashed peer |
| First message's expiry interval = exchange timeout | Misuses message expiry; risks long broker retention |
| Send a terminal status on local timeout | Needs its own delivery budget; may arrive after the peer is already terminal |

---

## Further reading

- **ADR:** `doc/dev/adr/0025-rpc-streaming.md` — normative, includes the full `__stream` grammar
  and the illustrative .NET API
- **Diagrams:** `doc/dev/adr/0025-rpc-streaming-lifecycle-diagrams.md` — 10 rendered Mermaid diagrams
  (shared lifecycle, establishment, full duplex, timeout, both cancellation directions, protocol
  error, fatal failure, packet classification, `__stream` anatomy)
- **Related ADRs:** 0006 decoupled caching · 0007 HLC timestamp · 0008 protocol split ·
  0009 protocol error structure · 0016 response topic pattern
- **Reference docs:** `doc/reference/command-timeouts.md` · `doc/reference/shared-subscriptions.md` ·
  `doc/reference/session-client.md`

<!--
If discussion goes deep on a diagram, switch to the lifecycle diagrams file in VS Code
preview rather than trying to describe it from the deck.
-->
