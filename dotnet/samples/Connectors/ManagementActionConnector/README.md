# ManagementActionConnector

A sample connector that exercises the .NET **Management Action API** end-to-end
against the real MQTT broker, **without a real southbound device**. A shared
in-process [`FakeDevice`](Devices/FakeDevice.cs) stands in for the device, and
three typed handlers — one per `AssetManagementGroupActionType` — implement the
business logic.

## Files

- [`Program.cs`](Program.cs) — host + DI registration. Registers `FakeDevice`
  as a singleton (so writes are observable across handler instances) plus the
  factory and worker.
- [`ManagementActionConnectorWorkerSample.cs`](ManagementActionConnectorWorkerSample.cs) —
  thin `BackgroundService` wrapper that owns an
  `Azure.Iot.Operations.Connector.ConnectorWorker` configured with an
  `IManagementActionHandlerFactory` and runs its `RunConnectorAsync` loop.
- [`Devices/FakeDevice.cs`](Devices/FakeDevice.cs) — in-memory device simulator
  with testability knobs (`SimulatedLatency`, `ForceFailure`,
  `MaxConcurrentOperations`, `InFlightGate`) so tests can deterministically
  exercise the SDK's cancellation, drain, exception-translation, and
  concurrency paths.
- [`Handlers/`](Handlers) — one handler per action type:
  - [`IdentifyHandler.cs`](Handlers/IdentifyHandler.cs) — Call action; parses an
    `IdentifyRequest`, asks `FakeDevice` to blink its locator indicator, returns
    an `IdentifyResponse`. Demonstrates `ManagementActionApplicationError` for an
    out-of-range `blinkCount`.
  - [`ReadTemperatureHandler.cs`](Handlers/ReadTemperatureHandler.cs) — Read
    action; samples the device. The `unit` reflects the most recent
    `write-configuration`.
  - [`WriteConfigurationHandler.cs`](Handlers/WriteConfigurationHandler.cs) —
    Write action; validates a `ConfigurationUpdate`, applies it to the device,
    returns a `ConfigurationAck`. Demonstrates `ValidationFailed` for
    out-of-range payloads.
  - [`SampleManagementActionHandlerFactory.cs`](Handlers/SampleManagementActionHandlerFactory.cs) — dispatches by action name.
- [`Contracts/Contracts.cs`](Contracts/Contracts.cs) — JSON request/response DTOs.
- [`KubernetesResources/`](KubernetesResources/) — `ConnectorTemplate` +
  `Device` + `Asset` CRs. The asset declares **all three** action types under
  one management group `device-control`.
- [`connector-metadata.json`](connector-metadata.json) — connector metadata
  conforming to the AIO connector schema; used by the
  `CI-ConnectorMetadataSchemaValidation` workflow.
- [`deploy-connector-and-device.sh`](deploy-connector-and-device.sh) — builds
  the container image, imports it into k3d, and applies the CRs. **No
  southbound server to deploy** — that's the whole point of this sample.

## Run / Deploy

```bash
cd dotnet/samples/Connectors/ManagementActionConnector
./deploy-connector-and-device.sh
```

This is run automatically by the [`CI-Dotnet`](../../../../.github/workflows/ci-dotnet.yml) workflow alongside the other sample connectors.

## Tests

End-to-end integration tests live in
[`dotnet/test/Azure.Iot.Operations.Connector.IntegrationTests/ManagementActionConnectorTests.cs`](../../../test/Azure.Iot.Operations.Connector.IntegrationTests/ManagementActionConnectorTests.cs).
They invoke each action over MQTT 5 RPC against the deployed connector pod and
validate the response payload + status headers.


## What this sample does *not* exercise

Because there is no real southbound process:

- **Real network I/O from the connector pod** (DNS, TLS to a southbound, retry
  policies, connection pooling). Covered by
  [`PollingRestThermostatConnector`](../PollingRestThermostatConnector/) and
  [`EventDrivenTcpThermostatConnector`](../EventDrivenTcpThermostatConnector/).
- **`EndpointCredentials` consumption** — the factory receives credentials but
  the in-process simulator ignores them.
- **Realistic latency under load** — the simulator's latency is an artificial
  `Task.Delay`. Use `FakeDevice.SimulatedLatency` to make timing-sensitive
  tests deterministic.

Everything between the broker and the SDK's
`ConnectorWorker` + `ManagementActionOrchestrator` (lifecycle, executor management, type
dispatch, `ApplicationError` round-trips, metadata/CloudEvent propagation,
ADR status reporting, drain-and-dispose) **is** covered.
