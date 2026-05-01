# ManagementActionConnector

Minimal connector sample for the **Management Action API** that landed on
branch `maxim/management-action`.

The sample exercises the user-facing
[`IManagementActionHandler`](../../../src/Azure.Iot.Operations.Connector/IManagementActionHandler.cs)
+ [`IManagementActionHandlerFactory`](../../../src/Azure.Iot.Operations.Connector/IManagementActionHandlerFactory.cs)
+ [`ManagementActionConnectorWorker`](../../../src/Azure.Iot.Operations.Connector/ManagementActionConnectorWorker.cs)
shape end-to-end. The handler implementation is intentionally a no-op
(logs the invocation, returns an empty success response) so the sample
stays focused on the wiring.

## Files

- [`Program.cs`](Program.cs) — host + DI registration. Registers the
  factory as a singleton and the worker as a hosted service.
- [`ManagementActionConnectorWorker.cs`](ManagementActionConnectorWorker.cs) —
  three small types in one file:
  - `ManagementActionConnectorWorkerSimplified` — a thin
    `BackgroundService` that owns a `ManagementActionConnectorWorker`
    instance and runs its `RunConnectorAsync` loop.
  - `SampleManagementActionHandlerFactory` — implements
    `IManagementActionHandlerFactory`. Logs and returns a
    `SampleManagementActionHandler` for every action.
  - `SampleManagementActionHandler` — implements `IManagementActionHandler`.
    Logs and returns an empty success response from each of
    `HandleCallAsync` / `HandleReadAsync` / `HandleWriteAsync`.
- [`appsettings*.json`](appsettings.json) — default logging config.
- [`KubernetesResources/`](KubernetesResources/) — test `Device` + `Asset`
  CRs. The `Asset` declares one management group (`device-control`) with
  one `Call` action (`reboot`). See that folder's README for details.

The sample uses the SDK's built-in
[`NoMessageSchemaProvider`](../../../src/Azure.Iot.Operations.Connector/NoMessageSchemaProvider.cs)
because there are no datasets or events.

## Run

```powershell
dotnet run --project samples/Connectors/ManagementActionConnector
```

Running outside of the AIO environment will fail while reading MQTT
connection settings — this is expected. Deploy to a cluster (or wire up
mock ADR files) once you want to drive real invocations.

## Why is the handler a no-op?

The branch lands the **Part 1 invocation pipeline** as scaffolding (see
[`management-action-design-onepager.md`](../../../../doc/dev/tmp/management-action-design-onepager.md)
*"Implementation status"*). The orchestrator
(`ManagementActionConnectorWorker`) is fully written, but the methods it
calls on `AssetClient` / `ManagementActionExecutor` /
`ManagementActionRequest` are still `NotImplementedException` stubs, so
the sample throws on the first action. Once those bodies land the same
sample will start delivering invocations to the handler without
modification.

## Background reading

The deeper "how does this all fit together" Q&A — lifecycle, runtime
model, where `ManagementActionExecutor` lives, how the type binding
works, asset shape vs connector binary cardinalities — used to live in
this README. It moved into the design doc so the explanations don't
drift away from the actual API:

- [`management-action-design-onepager.md`](../../../../doc/dev/tmp/management-action-design-onepager.md) — short overview + implementation status
- [`management-action-implementation-design.md`](../../../../doc/dev/tmp/management-action-implementation-design.md) — full design (see *Background & FAQ* section for the lifted Q&A)
- [`management-action-gap-analysis.md`](../../../../doc/dev/tmp/management-action-gap-analysis.md) — Rust ↔ .NET parity
