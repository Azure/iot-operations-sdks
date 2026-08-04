# Chunking: A vs B vs C


## The three in one line each

| | Where chunking lives | What it is a property of |
| --- | --- | --- |
| **A** | MQTT session client, **below** the envoys | the **transport** — any oversized PUBLISH, anywhere |
| **B** | A chunked envoy pair riding the **existing `__stream` wire** | the **command**, expressed as a streaming exchange |
| **C** | A chunked envoy pair with its **own `__chunk` wire** | the **command**, expressed as its own exchange type |

```
   Option A                  Option B                  Option C
 ┌──────────────┐         ┌───────────────┐         ┌──────────────┐
 │ application  │         │ application   │         │ application  │
 ├──────────────┤         ├───────────────┤         ├──────────────┤
 │ RPC │ Telem  │         │ RPC │ Telem   │         │ RPC │ Telem  │
 │     envoys   │         │  ┌─────────┐  │         │  ┌────────┐  │
 │              │         │  │chunked  │  │         │  │chunked │  │
 │              │         │  │ envoy   │  │         │  │ envoy  │  │
 │              │         │  ├─────────┤  │         │  │(own    │  │
 │              │         │  │streaming│  │         │  │ wire)  │  │
 │              │         │  │  wire   │  │         │  └────────┘  │
 ├──────────────┤         ├──┴─────────┴──┤         ├──────────────┤
 │ ▓ CHUNKING ▓ │         │ session clnt  │         │ session clnt │
 ├──────────────┤         ├───────────────┤         ├──────────────┤
 │ session clnt │         │               │         │              │
 └──────────────┘         └───────────────┘         └──────────────┘
   everything is           chunking is one          chunking is its
   chunkable, always       flavour of streaming     own thing
```

## Dimension by dimension

| | **A — transport** | **B — on streaming** | **C — own wire** |
| --- | --- | --- | --- |
| **Who chooses; what the app sees** | SDK at runtime from payload size; app sees nothing | API author at design time; app picks a chunked envoy | API author at design time; app picks a chunked envoy |
| **Old peers** | receive fragments they cannot parse → **break** | unaffected — new pattern, own protocol version | unaffected — new pattern, own protocol version |
| **Failure detection and reporting** | index gap or expiry lapse, but **cannot tell the sender** — no correlation, no reply path | index gap, holes before `last`, or exchange timeout; status + cancel on the exchange | index gap against a known total, or expiry lapse; the RPC correlation replies both ways |
| **Cancellation / backpressure / progress** | ✗ none | **✓ all three, already specified** | must be designed, or forgone |
| **Receiver-side state** | buffers with no context — a message id and a byte count; a partial redelivery has no replay-cache entry to hit | command, correlation, declared type, peer; inherits streaming's "no replay cache" stance | command, declared type, peer; replay cache designed alongside it |
| **WoT / codegen impact** | **none** — not an interaction pattern | new interaction type in the model | a flag on an existing command |
| **Must ship first** | nothing | **the whole streaming protocol** | nothing |
| **Protocols to specify, version and test** | 2, if streaming also ships | 1 — but the *full* streaming wire, most of it unused by chunking | **1** if streaming never ships; 2 if it does |
| **Blast radius if wrong** | **every peer on every topic** | streaming users only | users of chunked commands only |

## The two constraints that decide it

Both are settled.

| # | Constraint | What it settles |
| --- | --- | --- |
| **1** | **Deployed non-SDK peers must keep working** | **A is eliminated.** Versioning improves A's failure mode but cannot save the request direction — see below |
| **2** | **Streaming has no customer demand** | **B must fund the entire `__stream` wire out of chunking's budget** |


### Why constraint 1 ends Option A

**Protocol versioning helps, but not enough.** 
- Chunking is a major bump. A conforming peer rejects with 505 — clean, but not working.
- A hand-rolled peer such as DSS may not return 505 at all.
- Requests go out before the executor's version is known. A probe costs a round trip and
  cannot carry the oversized message.
- Shared subscriptions route successive invocations to different fleet members, so no
  version answer is cacheable.
- Gating on version puts envoy state in the transport. Gated, A is in-band negotiation
  for responses and C for requests.

### Why constraint 2 reverses the protocol-cost argument

B's case was always *"one wire protocol instead of two."* That accounting only holds if
streaming ships for its own reasons. It does not:

| | If streaming ships | If it never ships (**constraint 2**) |
| --- | --- | --- |
| **B** maintains | 1 protocol | 1 protocol — but the *full* streaming wire, including `last`, the control and status forms, tombstones, the cancel handshake and interleaving, none of which chunking uses |
| **C** maintains | 2 protocols | **1 protocol** — a small, strictly verifiable `__chunk` wire |

So the dimension that most favoured B now favours C. B does not save a protocol; it
substitutes a large one for a small one, and pays the whole streaming specification,
versioning and METL cost up front before any chunking can ship.

### Net effect

Constraint 1 removes A outright, and constraint 2 removes B's motivation. **C is what
the two leave standing** — see the bottom line for what that costs.

### Why request chunking hands C its capability story

The request-direction problem above is not specific to versioning. It applies to any
in-band negotiation: a marker on the request tells the executor what the *invoker*
supports, never the reverse.

A separate command solves it with no new mechanism. Only chunk-capable executors
subscribe to the chunked topic, so if none is deployed the invoker's first fragment
returns `no matching subscribers` and it learns the capability is absent — exactly the
mechanism ADR 25 already specifies for `NoAvailableStreamingExecutor`. Mixed old/new
fleets are fine; the chunked topic simply has fewer subscribers.


### The simplification that survives every scope change

One property is unaffected by both constraints and by every scope change, and it is
worth stating in the room: **the exchange is strictly sequential, never interleaved.** The
executor cannot begin responding until the full request is reassembled and deserialized
into its declared type. That is the single largest difference from streaming, and it
holds regardless of how many directions get chunked.

## What each option is optimizing for

- **A optimizes for zero API surface.** It is the only option where the answer to *"which
  API do I use when I don't know how big the payload is?"* is *"the one you already
  use."* Everything else it gives up — compatibility, failure reporting, memory
  bounding — is the price of that one property. *Moot: constraint 1 rules it out.*
- **B optimizes for one wire protocol.** Specification, versioning, test coverage and
  documentation cost real engineering time, and B pays it once instead of twice.
  *Weakened: constraint 2 makes it one large protocol instead of one small one.*
- **C optimizes for a minimal, fully verifiable wire and independent delivery.** Because
  the total is known before the first fragment ships, C can guarantee completeness in a
  way neither of the others can express as cheaply. *Unaffected by both.*

## A nuance worth settling in the room

**"Always chunked" vs "chunk when needed"** cuts across all three and is usually
conflated with the A/C choice:

- **Chunk when needed** (A's model): engage only when the payload exceeds the limit. A
  10-byte payload goes out exactly as it does today. Great ergonomics — but the sender
  must know whether the *receiver* can handle fragments, because the wire changes
  shape mid-flight. That is capability negotiation, and the request direction is where
  it breaks down.
- **Always chunked by declaration** (C's natural model): a command declared chunked
  is always chunked, even at `totalChunks = 1`. One code path, no negotiation, no
  capability probing — at the cost of a slightly larger header on small payloads and a
  separate command in the model.

This matters because **C's "you must pick a new API" cost is a modelling choice, not a
protocol requirement.** If the chunk-capable envoy becomes the default envoy for new
models, developers never choose — which reclaims most of A's ergonomic advantage
without A's compatibility break. Old models keep using the old envoy on the old wire.

## Bottom line

| | A | B | C |
| --- | --- | --- | --- |
| Best ergonomics | ★ | | |
| Lowest total protocol cost | | | **★** |
| Lowest compatibility risk | | ★ | **★** |
| Ships soonest | ★ | | **★** |
| Safest under load / attack | | ★ | **★** |
| **Survives both constraints** | **✗** | partly | **✓** |

**C is the answer under the current scope** — RPC-only, one logical payload per
direction, both directions chunkable, strictly sequential.

- **A is eliminated** by constraint 1. Deployed non-SDK peers must keep working and A has
  no opt-in to offer them. Its ergonomic advantage dies with it.
- **B is viable but unmotivated.** Every advantage it had rested on streaming shipping
  anyway; constraint 2 removes that premise and leaves a larger wire and a longer critical
  path with nothing to show for either.
- **C ships the smallest sufficient wire**, on its own schedule, without breaking anyone,
  and the separate command it needs doubles as request-direction capability discovery.

**Revisit B only if streaming acquires a real customer scenario.** At that point the
"one protocol" argument becomes real again, and the overlap between C's wire and
`__stream` — `$partition`, in-band timeout, index de-dup, two-sided buffering — is worth
re-examining before two protocols become permanent.

## Appendix: what C looks like in C#

The point of the sketch is how little there is. `InvokeCommandAsync` is unchanged from
`CommandInvoker<TReq, TResp>` and `OnCommandReceived` is unchanged from
`CommandExecutor<TReq, TResp>`; chunking shows up as three properties and one new error.

```csharp
public abstract class ChunkedCommandInvoker<TReq, TResp> : IAsyncDisposable
    where TReq : class
    where TResp : class
{
    public string RequestTopicPattern { get; init; }
    public Dictionary<string, string> TopicTokenMap { get; protected set; }

    // Advertised on every request so the executor can reject before sending fragments
    // that would only be dropped.
    public long MaxReassembledPayloadSize { get; set; } = 64 * 1024 * 1024;

    public Task<ExtendedResponse<TResp>> InvokeCommandAsync(
        TReq request,
        CommandRequestMetadata? metadata = null,
        Dictionary<string, string>? additionalTopicTokenMap = null,
        TimeSpan? commandTimeout = default,
        CancellationToken cancellationToken = default);
}

public abstract class ChunkedCommandExecutor<TReq, TResp> : IAsyncDisposable
    where TReq : class
    where TResp : class
{
    public required Func<ExtendedRequest<TReq>, CancellationToken, Task<ExtendedResponse<TResp>>>
        OnCommandReceived { get; set; }

    public string RequestTopicPattern { get; init; }
    public string ServiceGroupId { get; init; }
    public TimeSpan ExecutionTimeout { get; set; }

    // The executor buffers whatever peers push at it, so these are the DoS bounds.
    public long MaxReassembledPayloadSize { get; set; } = 64 * 1024 * 1024;
    public int MaxConcurrentReassemblies { get; set; } = 16;
}
```

Calling it is ordinary unary RPC:

```csharp
await using var invoker = new GetSchemaCommandInvoker(appContext, mqttClient);

try
{
    ExtendedResponse<GetSchemaResponse> resp =
        await invoker.InvokeCommandAsync(new GetSchemaRequest { SchemaId = id });

    Use(resp.Response.Content);   // reassembled, deserialized, declared type
}
catch (AkriMqttException ex) when (ex.Kind == AkriMqttErrorKind.ChunkTransferIncomplete)
{
    // A fragment was lost or expired. Nothing partial is ever surfaced — re-invoke.
}
```

`putSchema` looks identical with a large request; the invoker fragments it without the
caller knowing. New `AkriMqttErrorKind` members: `ChunkTransferIncomplete`,
`ChunkIntegrityFailed`, `PayloadTooLarge`, and `NoAvailableChunkedExecutor` — the last
being where `no matching subscribers` capability discovery surfaces to the user.

**Notably absent:** `IAsyncEnumerable`, per-fragment types, indices, acknowledgements.
That shape belongs to streaming, and C not needing it is the whole argument.

**Still to settle:** whether the size bounds sit per envoy or as one policy on
`ApplicationContext`; whether the replay cache holds the assembled response or the
fragment set; whether a 200 MB transfer warrants `IProgress<long>`, which unary RPC has
nowhere to put; and whether the chunked and unary envoys share a base class so a service
exposing both does not duplicate its topic configuration.
