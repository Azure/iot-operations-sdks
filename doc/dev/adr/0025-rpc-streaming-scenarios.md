# ADR 25: Scenario Coverage

A reading guide: for each item in the PM paper *Streaming Responses in the AIO SDK*, the sections of [ADR 25](./0025-rpc-streaming.md) that address it. **Partly** marks an item covered with a stated limit.

All four scenarios are one request answered by a stream of responses; in ADR 25 terms, a request stream of one entry followed by `last`.

## Problem statement and desired SDK experience

| Paper item | Read | How |
| ---- | ---- | ---- |
| Bespoke chunk identifiers, completion markers, correlation | [`__stream`](./0025-rpc-streaming.md#streaming-user-property), [completion](./0025-rpc-streaming.md#exchange-completion), [routing](./0025-rpc-streaming.md#topics-and-routing) | The protocol owns the entry index, `last`, and the correlation GUID |
| Requests over 10 minutes (**Partly**) | [Timeout](./0025-rpc-streaming.md#timeout-support), [alternatives](./0025-rpc-streaming.md#alternative-timeout-designs-considered) | One total exchange budget set by the caller; a crashed peer is noticed only when it runs out; an inactivity timeout is set aside |
| `await foreach (var item in client.ExecuteStreamingAsync(request))` | [Invoker](./0025-rpc-streaming.md#invoker-behavior), [errors](./0025-rpc-streaming.md#reporting-errors-to-the-application) | SDK shortcut: one request in, a plain async sequence out; terminal errors are raised from the loop |

## Representative scenarios

| Scenario | Read | How |
| ---- | ---- | ---- |
| 1. Incremental OPC UA browse | [Requirements](./0025-rpc-streaming.md#requirements), [producing](./0025-rpc-streaming.md#producing-and-consuming) | Each node is an entry sent when found; no total count needed up front |
| 2. Historic read / backfilling (**Partly**) | [Producing](./0025-rpc-streaming.md#producing-and-consuming), [recovery](./0025-rpc-streaming.md#disconnection-and-recovery), [stream failure](./0025-rpc-streaming.md#stream-failure) | Entries arrive in order with their index; a source failure ends the stream with an application error after the records already sent. A lost entry or executor restart is left to the application by design (index and timestamp on each entry); an opt-in SDK setting to fail the stream on either is planned |
| 3. W3C WoT TD streaming | [Requirements](./0025-rpc-streaming.md#requirements) | One TD per entry as each device is discovered; every entry has the same payload type |
| 4. OpenUSD artefact download | [Requirements](./0025-rpc-streaming.md#requirements), [producing](./0025-rpc-streaming.md#producing-and-consuming) | The scene streams as self-contained USD layers, one per entry; the consumer composes each onto its stage as it arrives, so a partial scene renders before the stream ends (POC: `OpenUsdSceneComposition`) |

## Platform requirements

| Requirement | Read | How |
| ---- | ---- | ---- |
| Correlation | [Routing](./0025-rpc-streaming.md#topics-and-routing) | One SDK-generated correlation GUID on every message of the exchange |
| Ordering | [Producing](./0025-rpc-streaming.md#producing-and-consuming) | Responses use the invoker's own (non-shared) topic, so MQTT delivers them in publish order; a loss leaves a gap but never reorders |
| Completion | [Completion](./0025-rpc-streaming.md#exchange-completion), [why `last`](./0025-rpc-streaming.md#last-message-being-its-own-message) | Standalone `last` control message; the consumer's sequence simply completes |
| Error propagation | [Stream failure](./0025-rpc-streaming.md#stream-failure), [status codes](./0025-rpc-streaming.md#status-codes), [errors](./0025-rpc-streaming.md#reporting-errors-to-the-application) | `Failed` (`500`), with ADR 21 `AppErrCode` for an application error; the consumer's stream ends with the error after the earlier entries |
| Back-pressure | [Flow control](./0025-rpc-streaming.md#flow-control) | The next entry is taken only after the broker accepted the previous one; a bounded consumer buffer ends the exchange with `Overflow` (`429`) when full; a consumer sets the pace via the request stream |
| Payload fidelity | [Producing](./0025-rpc-streaming.md#producing-and-consuming), [requirements](./0025-rpc-streaming.md#requirements) | Each entry is one MQTT message carrying one serialized item, so items arrive exactly as emitted, never merged or split |

The .NET POC exercises these in [`StreamingPocTests`](../../../dotnet/test/Azure.Iot.Operations.Protocol.IntegrationTests/).
