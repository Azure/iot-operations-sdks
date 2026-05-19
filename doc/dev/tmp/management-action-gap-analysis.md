# Management Action: Gap Analysis (.NET vs Rust)

**Date:** 2026-04-13  
**Reference:** [Rust API Docs - ManagementActionClient](https://docs.rs/azure_iot_operations_connector/2.0.0-rc3/azure_iot_operations_connector/base_connector/managed_azure_device_registry/struct.ManagementActionClient.html)

---

## What is a Management Action?

A **Management Action** in Azure IoT Operations represents a callable operation (read/write/call) on an asset, organized within **Management Groups** in the Azure Device Registry (ADR). It functions as an RPC endpoint exposed by a connector — external callers (the cloud, other services) can invoke actions on physical assets/devices through the connector.

The Rust `ManagementActionClient` is the connector-side lifecycle manager for a single management action. It provides:

| Capability | Description |
|---|---|
| **Receive invocations** | Via `ManagementActionExecutor.recv_request()` — receives RPC requests over MQTT |
| **Send responses** | Via `request.complete(response)` using `ManagementActionResponseBuilder` |
| **Lifecycle notifications** | `recv_notification()` — Updated, UpdatedWithNewExecutor, AssetUpdated, Deleted |
| **Schema registration** | Report request/response message schemas to ADR/Schema Registry |
| **Status & health reporting** | Configuration status + runtime health events |
| **Executor management** | Automatic executor creation/replacement when topics/definitions change |

---

## Current State by Language

### Rust — Fully Implemented

Full management action support at both the services and connector layers:

- **ManagementActionExecutor** — wraps `rpc_command::Executor<BypassPayload, BypassPayload>` to subscribe to and receive RPC requests over MQTT
- **ManagementActionRequest** — incoming request with `complete()`, `is_cancelled()`, `raw_payload()`, metadata accessors
- **ManagementActionResponseBuilder** — builds responses with `payload()`, `content_type()`, `application_error()`, `cloud_event()`, etc.
- **ManagementActionClient** — full lifecycle management with `recv_notification()` returning Updated/UpdatedWithNewExecutor/AssetUpdated/Deleted
- **ManagementActionStatusReporter** — configuration status + runtime health events
- **Message schema reporting** — `report_request_message_schema_if_modified()` / `report_response_message_schema_if_modified()`
- **Two sample connectors** demonstrating management actions end-to-end

### .NET — Partially Implemented (Health/Status Reporting Only)

- **Runtime health reporting** — `AssetClient.ReportManagementActionRuntimeHealthAsync()` (batch and single-action)
- **Periodic background reporting** with deduplication via `AssetRuntimeHealthReporter`
- **Pause/resume** — `PauseReportingManagementActionAsync()`
- **Generated schema/telemetry types** — `ManagementActionRuntimeHealthEventTelemetrySender`, etc.

> **Note:** .NET does not use per-dataset/management-group/event clients like Rust. It uses asset-level clients (`AssetClient`) which should allow the user to do whatever is defined in the Rust API.

### Go — Not Implemented

Zero references to management actions anywhere in the `go/` directory.

---

## Feature Gap Matrix

| Capability | Rust | .NET | Go |
|---|---|---|---|
| Runtime health reporting | **Yes** | **Yes** | No |
| Periodic health with dedup | **Yes** | **Yes** | No |
| Pause/resume health reporting | **Yes** | **Yes** | No |
| ManagementActionExecutor (receive RPC requests) | **Yes** | **Missing** | No |
| ManagementActionRequest (incoming request type) | **Yes** | **Missing** | No |
| ManagementActionResponse/Builder (response construction) | **Yes** | **Missing** | No |
| ManagementActionClient (lifecycle manager) | **Yes** | **Missing** | No |
| ManagementActionNotification (updates/deletes) | **Yes** | **Missing** | No |
| ManagementActionRef (identifier type) | **Yes** | **Missing** | No |
| Connector lifecycle integration (per-action tasks) | **Yes** | **Missing** | No |
| Request/response message schema reporting | **Yes** | **Missing** | No |
| Application error responses to invoker | **Yes** | **Missing** | No |
| Executor draining on definition change | **Yes** | **Missing** | No |
| Automatic error response on request drop | **Yes** | **Missing** | No |
| Sample connector using management actions | **Yes** (2) | **Missing** | No |

---

## What Needs to Be Built in .NET

### 1. Handling Management Action Invocations — NOT SUPPORTED

Build a .NET equivalent of Rust's `ManagementActionExecutor` + `ManagementActionRequest` + `ManagementActionResponseBuilder`. 

.NET already has the building block `CommandExecutor<TReq, TResp>` in `Azure.Iot.Operations.Protocol` — the management action executor would wrap it, similar to how Rust wraps `rpc_command::Executor`.

**Rust reference:** `rust/azure_iot_operations_connector/src/management_action_executor.rs`

Key types needed:
- `ManagementActionExecutor` — wraps `CommandExecutor`, provides `RecvRequestAsync()` 
- `ManagementActionRequest` — provides `CompleteAsync(response)`, `IsCancelled`, `RawPayload`, `ContentType`, metadata
- `ManagementActionResponse` / `ManagementActionResponseBuilder` — builds responses with payload, content type, cloud event, application error
- `ManagementActionApplicationError` — error code + payload for reporting execution failures

### 2. Registering Request/Response Message Schemas — NOT SUPPORTED

Build equivalents of:
- `report_request_message_schema_if_modified()` 
- `report_response_message_schema_if_modified()`
- `report_request_message_schema_reference_if_modified()`
- `report_response_message_schema_reference_if_modified()`

These use the Schema Registry Service to register full schemas or report pre-existing schema references to ADR, with built-in retry logic (10 retries, exponential backoff + jitter).

**Rust reference:** `ManagementActionClient` methods in `rust/azure_iot_operations_connector/src/base_connector/managed_azure_device_registry.rs`

### 3. Report Health Status of Management Actions — ALREADY SUPPORTED

`AssetClient.ReportManagementActionRuntimeHealthAsync()` exists with:
- Batch variant: `ReportManagementActionRuntimeHealthAsync(List<ConnectorManagementActionsRuntimeHealthEvent>, ...)`
- Single-action variant: `ReportManagementActionRuntimeHealthAsync(string managementGroupName, string managementActionName, ConnectorRuntimeHealth, ...)`
- Background periodic reporting with deduplication in `AssetRuntimeHealthReporter`
- Pause/resume via `PauseReportingManagementActionAsync()`

**Key .NET files:**
- `dotnet/src/Azure.Iot.Operations.Connector/AssetClient.cs` (lines 292-337)
- `dotnet/src/Azure.Iot.Operations.Services/AssetAndDeviceRegistry/AssetRuntimeHealthReporter.cs`

### 4. Report Management Action Status — ALREADY SUPPORTED

Configuration status reporting exists via `AssetRuntimeHealthReporter` and the generated telemetry senders.

> **Note on "forwarding":** Earlier revisions of this doc listed a fifth bucket
> — *"Forwarding Management Action Messages to Broker/BSS"* — as a missing
> feature. Verified against the Rust source
> (`rust/azure_iot_operations_connector/src/base_connector/managed_azure_device_registry.rs`,
> 2.0.0-rc3): **no such feature exists in Rust.** `ManagementActionClient`
> exposes only schema reporting, `recv_notification`, status/health
> reporting, and read-only accessors. The `forward_data` / `forward_data_provide_protocol_specific_identifier`
> methods are on `DataOperationClient` (datasets/events/streams), **not** on
> `ManagementActionClient`, and `destination_endpoint.rs` has zero
> references to management actions. Management actions are inbound RPC:
> the response goes back to the invoker over the same RPC reply topic via
> the underlying `rpc_command::Executor` / `CommandExecutor` — there is
> no outbound forwarding path to the broker or broker state store to
> build. The real work is fully covered by bucket #1.

---

## Rust Management Action Workflow (Reference for .NET Implementation)

The Rust samples show the complete workflow:

### Step 1: Discovery
`BaseConnector` discovers management action definitions from ADR and delivers them as `(ManagementActionClient, Result<ManagementActionExecutor>)` tuples through the asset notification channel:
```
BaseConnector
  -> DeviceEndpointClient (recv_notification -> AssetClient)
    -> AssetClient (recv_notification -> AssetComponentClient::ManagementAction(client, executor))
```

### Step 2: Handler Spawning
Each management action spawns its own async handler task that runs a `select!` loop with three concurrent arms:
- Device endpoint readiness changes
- Management action lifecycle notifications
- Incoming RPC requests (guarded by executor availability)

### Step 3: Receiving Requests
```rust
// Requests arrive via the executor
let request = executor.recv_request().await; // Returns Option<ManagementActionRequest>
// None = executor shut down, Some = new request
```

### Step 4: Sending Responses
```rust
// Success
let response = ManagementActionResponseBuilder::default()
    .payload(payload_bytes)
    .content_type("application/json".to_string())
    .cloud_event(None)
    .build()?;
request.complete(response).await;

// Error
let response = ManagementActionResponseBuilder::default()
    .application_error(ManagementActionApplicationError {
        application_error_code: "SomeErrorCode".to_string(),
        application_error_payload: "Description".to_string(),
    })
    .payload(vec![])
    .content_type("application/json".to_string())
    .cloud_event(None)
    .build()?;
request.complete(response).await;
```

### Step 5: Lifecycle Events
```rust
match management_action_client.recv_notification().await {
    UpdatedWithNewExecutor(Ok(new_executor)) => {
        drain_executor(old_executor).await;  // respond to stale requests with errors
        current_executor = Some(new_executor);
        // Persist acceptance of the new definition into config status
        // (AssetManagementGroupActionStatus.Error = None) via UpdateAssetStatusAsync.
    }
    Updated(Ok(())) => {
        // Re-validate definition, update internal state, AND
        // write outcome to AssetManagementGroupActionStatus.Error
        // via UpdateAssetStatusAsync (None on success).
    }
    AssetUpdated(Ok(())) => { /* parent asset changed */ }
    Updated(Err(e)) | AssetUpdated(Err(e)) => {
        // Mark invalid by writing the ConfigError into
        // AssetManagementGroupActionStatus.Error via UpdateAssetStatusAsync.
    }
    Deleted => {
        drain_executor(current_executor).await;
        break; // exit handler
    }
}
```

### Step 6: Status & Health Reporting
```rust
// Configuration status
status_reporter.report_status_if_modified(...).await;

// Runtime health
status_reporter.report_health_event(RuntimeHealthEvent::Available);
status_reporter.report_health_event(RuntimeHealthEvent::Unavailable { message, reason_code });

// On definition change, pause health until re-validated
status_reporter.pause_and_refresh_health_version();
```

### Step 7: Schema Registration
```rust
management_action_client
    .report_request_message_schema_if_modified(|current_ref| {
        Some(MessageSchema { /* ... */ })
    }).await;

management_action_client
    .report_response_message_schema_if_modified(|current_ref| {
        Some(MessageSchema { /* ... */ })
    }).await;
// Must be re-reported on any definition update, even if unchanged
```

---

## Key Rust Reference Files

| File | Purpose |
|---|---|
| `rust/azure_iot_operations_connector/src/management_action_executor.rs` | Core executor: recv_request, request/response types, builder (~600 lines) |
| `rust/azure_iot_operations_connector/src/base_connector/managed_azure_device_registry.rs` | `ManagementActionClient`, lifecycle, notifications, schema reporting (~1500+ lines) |
| `rust/azure_iot_operations_connector/examples/base_connector_sample.rs` | Working sample: `run_management_action()` shows full flow (~800 lines) |
| `rust/sample_applications/sample_connector_scaffolding/src/main.rs` | Scaffolding template: `handle_management_action()` with `ActionState` machine (~1200 lines) |
| `rust/azure_iot_operations_connector/src/lib.rs` | `ManagementActionRef` definition |
| `rust/azure_iot_operations_services/src/azure_device_registry/client.rs` | Services-layer health reporting |

## Key .NET Files (Existing Implementation)

| File | Purpose |
|---|---|
| `dotnet/src/Azure.Iot.Operations.Connector/AssetClient.cs` | `ReportManagementActionRuntimeHealthAsync()` methods |
| `dotnet/src/Azure.Iot.Operations.Connector/ConnectorManagementActionsRuntimeHealthEvent.cs` | Health event DTO |
| `dotnet/src/Azure.Iot.Operations.Services/AssetAndDeviceRegistry/AssetRuntimeHealthReporter.cs` | Background health reporting with dedup |
| `dotnet/src/Azure.Iot.Operations.Services/AssetAndDeviceRegistry/IAzureDeviceRegistryClient.cs` | Service-layer interface |
| `dotnet/src/Azure.Iot.Operations.Services/AssetAndDeviceRegistry/AzureDeviceRegistryClient.cs` | Service-layer implementation |
| `dotnet/src/Azure.Iot.Operations.Protocol/RPC/CommandExecutor.cs` | Building block for management action executor |

---

## Suggested Implementation Approach

Since .NET uses asset-level clients rather than per-component clients, the management action capabilities could be exposed through the existing `AssetClient` or via a new `ManagementActionClient` alongside it. 

The Rust scaffolding template (`sample_connector_scaffolding`) is the best reference for what the end-to-end connector experience should look like. The MQTT connector's usage of management actions also serves as inspiration for a .NET sample connector.

Key decisions for .NET:
1. **Where to expose the executor** — on `AssetClient` or a new dedicated `ManagementActionClient`?
   - **Decision: On the existing `AssetClient` (revised after review).**
   - Original proposal was a dedicated `ManagementActionClient`, but senior review feedback preferred keeping all asset concerns in one place. Management action methods (`GetManagementActionExecutorAsync`, `RecvManagementActionNotificationAsync`, schema reporting) are added directly to `AssetClient` with `managementGroupName` + `managementActionName` parameters as identifiers. No new callback on `ConnectorWorker` — users handle management actions within the existing `WhileAssetIsAvailable` callback.
   - Health reporting remains on `AssetClient` (already implemented, consistent with datasets/events/streams pattern).
2. **Notification model** — how to surface lifecycle events (C# events, async enumerable, callback pattern)?
   - **Decision: `AssetClient.RecvManagementActionNotificationAsync(groupName, actionName)` with per-action `Channel<T>` backing.**
   - Fine-grained lifecycle notifications (Updated, UpdatedWithNewExecutor, AssetUpdated, Deleted) delivered via `RecvManagementActionNotificationAsync()`. Internally backed by `Channel<ManagementActionNotification>` per action. Users coordinate requests and notifications inside the existing `WhileAssetIsAvailable` callback via `Task.WhenAny` or similar select-style pattern.
   - Rationale: Reuses the existing callback surface users already know, while accommodating management action's unique per-action lifecycle (executor replacement, non-destructive updates).
3. **Response builder pattern** — fluent builder vs. constructor vs. record?
   - **Decision: `public record` with `required` properties (Option B).**
   - Rationale: The dominant codebase pattern — 45+ ADR model types use `public record` with `required` + `{ get; set; }` and object initializer construction. The `required` keyword provides compile-time enforcement of mandatory fields (payload, content_type, cloud_event), eliminating the need for runtime `Build()` validation. No public fluent builders exist in the .NET SDK. For application errors, a `.WithApplicationError()` convenience method can be added following the `ExtendedResponse<T>` precedent. The incoming request type will similarly be a record/class with read-only properties, consistent with `ExtendedRequest<T>`.
4. **Schema reporting integration** — extend `AssetClient` or new surface?
   - **Decision: On `AssetClient` (consistent with revised decision 1).**
   - Management actions have a request/response schema pair that is per-action, not per-asset. There is no data-forwarding trigger to piggyback on (unlike datasets/events where `ForwardSampledDatasetAsync` implicitly registers schemas via `ConnectorWorker`). Schema reporting is explicit — user calls `ReportManagementActionRequestMessageSchemaAsync` / `ReportManagementActionResponseMessageSchemaAsync` on `AssetClient` with group/action name parameters.
   - Note: This means management action schema reporting is explicit, while dataset/event schemas are implicit (registered as a side effect of forwarding). This inconsistency is acceptable — it reflects the real difference in data flow (inbound RPC vs outbound telemetry).
   - `AssetClient` gains a `SchemaRegistryClient` dependency (simple MQTT-backed client already used by `ConnectorWorker`, injected by `ConnectorWorker` during construction).
