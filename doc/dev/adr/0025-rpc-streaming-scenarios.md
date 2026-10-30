# ADR 25: Scenario Coverage

This document takes each case in the PM paper *Streaming Responses in the AIO SDK* and briefly describes how the protocol in [ADR 25](./0025-rpc-streaming.md) addresses it.

Status key:

- **Met**: the ADR covers the case.
- **Partly**: the ADR covers it, with a stated limit.
- **Open**: the ADR does not cover it yet.

All four representative scenarios share one shape: a single request answered by a stream of responses. ADR 25 is bidirectional; this shape is the case where the request stream has one entry followed by `last`.

## Summary

| Case | Status | ADR 25 mechanism |
| ---- | ---- | ---- |
| [Incremental OPC UA browse](#1-incremental-opc-ua-browse) | Met | Responses sent as produced; no total count needed up front |
| [Historic read / backfilling](#2-historic-read--backfilling) | Partly | In-order delivery and an index on each entry; a lost entry or executor restart is detectable but not reported by the SDK |
| [W3C WoT TD streaming](#3-w3c-wot-td-streaming) | Met | One TD per entry, sent as discovered |
| [OpenUSD artefact download](#4-openusd-artefact-download) | Partly | Met for scene composition; splitting one file across messages is left to ADR 34 |
| [Desired SDK experience](#desired-sdk-experience) | Met | SDK shortcut: one request in, a plain async sequence out |
| [Long-running requests](#problem-statement) | Partly | Total exchange timeout chosen by the caller; no inactivity timeout |
| [Correlation](#correlation) | Met | One correlation GUID per exchange, owned by the SDK |
| [Ordering](#ordering) | Met | MQTT in-order delivery on the invoker's own response topic |
| [Completion](#completion) | Met | Standalone `last` control message |
| [Error propagation](#error-propagation) | Met | `Failed` status; the consumer's stream ends with an error |
| [Back-pressure](#back-pressure) | Met | Producer paced by broker acceptance; bounded consumer buffer ending in `Overflow`; consumer-set pace via the request stream |
| [Payload fidelity](#payload-fidelity) | Met | One entry per MQTT message |

## Problem statement

**Results that take more than 10 minutes.** Partly. The [exchange timeout](./0025-rpc-streaming.md#timeout-support) is one total budget that the caller picks per invocation, so a long request works if the caller sets a long enough budget. It is not an inactivity timeout: a crashed peer is noticed only when that budget runs out. An inactivity timeout with heartbeats was [considered and set aside](./0025-rpc-streaming.md#alternative-timeout-designs-considered) until a customer needs an unbounded stream.

**Ad-hoc chunk identifiers, completion markers, and correlation handling.** Met. The protocol owns all three: every entry carries its index in [`__stream`](./0025-rpc-streaming.md#streaming-user-property), the [`last`](./0025-rpc-streaming.md#exchange-completion) control message ends a stream, and one correlation GUID [identifies the exchange](./0025-rpc-streaming.md#topics-and-routing).

## Desired SDK experience

> `await foreach (var item in client.ExecuteStreamingAsync(request))`

Met. The [invoker behavior](./0025-rpc-streaming.md#invoker-behavior) allows an SDK shortcut that takes one request and returns the response payloads as a plain async sequence. The executor [supplies its responses as an async sequence](./0025-rpc-streaming.md#core-abstractions), and the SDK handles the protocol mechanics. Terminal errors are raised from that same loop (see [reporting errors](./0025-rpc-streaming.md#reporting-errors-to-the-application)).

The .NET POC implements this as `StreamingCommandInvoker.ExecuteStreamingAsync`; the four scenario tests below use it.

## Representative scenarios

### 1. Incremental OPC UA browse

*Browse results should be returned as they are discovered.*

Met. The executor sends each node as soon as it finds it; a producer [needn't know the total count](./0025-rpc-streaming.md#requirements) before its first entry. It ends the stream with `last` when the browse finishes. Memory stays bounded on the executor because nothing is buffered before sending.

POC: [`IncrementalOpcUaBrowse`](../../../dotnet/test/Azure.Iot.Operations.Protocol.IntegrationTests/StreamingPocTests.cs).

### 2. Historic read / backfilling

*A single request returns many records, and the consumer receives exactly what the producer emitted, in the same logical sequence.*

Partly.

- **Same sequence:** met. Responses arrive in the order the executor sent them (see [ordering](#ordering)).
- **Exactly what was emitted:** detectable, not enforced. An entry can be lost if its [message expiry](./0025-rpc-streaming.md#message-level-timeout) lapses at the broker. A replacement or restarted executor [restarts the response stream at index 0](./0025-rpc-streaming.md#disconnection-and-recovery). Each entry exposes its index and HLC timestamp, so the consumer can see a gap (skipped index) or a restart (index goes back, new node ID in the timestamp). The SDK does not yet fail the stream on either. The plain-payload shortcut hides both fields, so a caller using it cannot detect them. Making the SDK end the stream with an error in these cases is the remaining work.
- **Source failure mid-backfill:** met. The executor ends the stream with an application error ([`Failed`](./0025-rpc-streaming.md#stream-failure) with `AppErrCode`); the consumer receives every record sent before the failure, then the error. A single unreadable record stays an ordinary entry, and the stream continues.

POC: [`HistoricReadBackfill`](../../../dotnet/test/Azure.Iot.Operations.Protocol.IntegrationTests/StreamingPocTests.cs), [`StreamFailure_ExecutorApplicationError`](../../../dotnet/test/Azure.Iot.Operations.Protocol.IntegrationTests/StreamingPocTests.StreamFailure.cs).

### 3. W3C WoT TD streaming

*The exporter streams TD/TM documents as they are generated.*

Met. Each Thing Description is one entry, sent when its device is discovered. All entries have the [same payload type](./0025-rpc-streaming.md#requirements), here a TD document. A TD that doesn't fit in one MQTT message is the [payload fidelity](#payload-fidelity) limit below.

POC: [`WotThingDescriptionStreaming`](../../../dotnet/test/Azure.Iot.Operations.Protocol.IntegrationTests/StreamingPocTests.cs).

### 4. OpenUSD artefact download

*Ordered chunk streaming so large files can be reconstructed deterministically without a bespoke chunking protocol.*

Partly.

- **Scene composition:** met. Each prim or layer is a self-contained entry, so the consumer can render a partial scene before the stream ends (comment AK2R1).
- **One large file split across entries:** not covered by streaming. Splitting a single payload is a [non-requirement](./0025-rpc-streaming.md#non-requirements) because any entry may be lost and streaming does not check that it received everything. The entry index lets a higher layer detect a gap and re-request the file. Guaranteed reassembly is specified in [ADR 34](./0034-large-message-chunking.md) (comment AP1).

POC: [`OpenUsdSceneComposition`](../../../dotnet/test/Azure.Iot.Operations.Protocol.IntegrationTests/StreamingPocTests.cs).

## Platform requirements

### Correlation

*All streamed response messages belong to one logical request without user-defined correlation IDs.*

Met. One correlation GUID [identifies the whole exchange](./0025-rpc-streaming.md#topics-and-routing) and is carried on every message in both directions. The SDK generates it; the caller never has to.

### Ordering

*Consumers receive response items in the same logical order in which the executor emits them.*

Met. Responses travel on the invoker's own response topic, which is not a shared subscription, so MQTT delivers them in the order they were published. The consumer relies on that order: [new data after `last` is a protocol violation](./0025-rpc-streaming.md#producing-and-consuming). Loss can leave a gap but never reorders the remaining entries. The ADR's non-requirement about *intended* order refers to that loss (see [historic read](#2-historic-read--backfilling)).

### Completion

*A clear end-of-stream signal without application-level "last message" flags.*

Met. A producer ends its stream with a standalone [`last` control message](./0025-rpc-streaming.md#last-message-being-its-own-message) that carries no payload, so the application never adds a flag. The consumer's async sequence simply completes.

### Error propagation

*Failures are surfaced as stream errors, not hidden in ad-hoc payload conventions.*

Met. A producer ends its stream with a [`Failed` status](./0025-rpc-streaming.md#stream-failure) (`__stat` `500`). An application error carries the [ADR 21](./0021-error-modeling-headers.md) `AppErrCode` and optional `AppErrPayload`; an unhandled exception in the application's code is reported as an execution error. The consumer's stream [ends with an error](./0025-rpc-streaming.md#reporting-errors-to-the-application) after the entries that came before it, and the same surface reports cancellation, timeout, protocol violations, and `NoAvailableStreamingExecutor`.

POC: the three [`StreamFailure_*`](../../../dotnet/test/Azure.Iot.Operations.Protocol.IntegrationTests/StreamingPocTests.StreamFailure.cs) tests.

### Back-pressure

*The producer can respect downstream consumption rate and avoid unbounded buffering.*

Met, as narrowed by comments TT6 and AK7R6: the producer is the executor, no consumer signal is required, and the SDK accepts each write, retries until the timeout, then reports an error to the producer. [Flow control](./0025-rpc-streaming.md#flow-control) specifies:

- **Producer pacing.** The SDK takes the next entry from the producer only after the broker accepted the previous one. A message not accepted before the exchange timeout times the exchange out; one the broker rejects ends it with `Failed`. Either way the producer receives the error.
- **No unbounded buffering.** Each consumer holds received entries in a buffer with a configurable, finite limit. An entry that arrives while the buffer is full ends the exchange with an `Overflow` (`429`) status; the consumer's application gets the buffered entries, then the error, and the producer gets the overflow from the peer. The backlog between the two waits at the broker, bounded by message expiry and the broker's subscriber queue limit.
- **Consumer-set pace.** There is no credit signal ([non-requirement](./0025-rpc-streaming.md#non-requirements)). A consumer that needs to set the pace asks for the next batch on the request stream, and the executor answers with up to that many entries.

Holding MQTT acknowledgements is deliberately not used for pacing: acknowledgements go out in order per connection, so a slow stream would stall every other stream on that client.

POC: [`FlowControl_*`](../../../dotnet/test/Azure.Iot.Operations.Protocol.IntegrationTests/StreamingPocTests.FlowControl.cs) tests — producer pacing, overflow on each side, and consumer-paced pull.

### Payload fidelity

*Received item boundaries match the emitted item boundaries.*

Met. Each entry is exactly one MQTT message, deserialized as one item of the stream's single payload type, so boundaries are preserved. The limit is one entry per message: an item larger than the broker's maximum packet size needs [ADR 34](./0034-large-message-chunking.md) chunking (see [OpenUSD](#4-openusd-artefact-download)).

## Paper comments

| Comment | Point | ADR 25 |
| ---- | ---- | ---- |
| AP1 | OpenUSD download looks like chunking rather than streaming | Agreed for single large files: [non-requirement](./0025-rpc-streaming.md#non-requirements), see ADR 34 |
| AK2R1 | OpenUSD can compose scenes, so it is also streaming | Met: one self-contained entry per prim or layer |
| TT3 | What message loss can the platform accommodate? | An entry can be lost when its message expiry lapses at the broker or a session is lost; each entry's index makes the loss visible |
| TT4R3 | By default a stream allows loss; the recipient may cancel if that is unacceptable | Matches the ADR: no delivery guarantee, and either side can [cancel](./0025-rpc-streaming.md#cancellation-support) |
| AK5R3 | When a message expires at the broker, the client-side stream should fail with an error; the SDK should forward packets in order | In order: already met. Failing on a gap: open (see [historic read](#2-historic-read--backfilling)) |
| TT6 | Is a consumer "ready" signal expected, and who is the producer? | No signal; the producer is either side's stream, paced by the broker (see [back-pressure](#back-pressure)) |
| AK7R6 | The producer is the executor; no signal is needed; retry until timeout, then return an error to the producer | Met (see [back-pressure](#back-pressure)) |
