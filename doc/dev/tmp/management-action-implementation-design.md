# Management Action .NET Implementation — Design

**Date:** 2026-04-14  
**Prerequisite:** [Gap Analysis](management-action-gap-analysis.md)

---

## Class Diagrams

### Simplified: Relationships Overview

New types in green. Shows only how types relate — no internal details.

```mermaid
classDiagram
    direction TB

    %% Hosting / Base
    BackgroundService <|-- ConnectorBackgroundService
    ConnectorBackgroundService <|-- ConnectorWorker
    ConnectorWorker <|-- PollingTelemetryConnectorWorker

    %% Existing: ConnectorWorker creates event args
    ConnectorWorker ..> DeviceAvailableEventArgs : creates
    ConnectorWorker ..> AssetAvailableEventArgs : creates

    %% Existing: ConnectorWorker dependencies
    ConnectorWorker --> IAzureDeviceRegistryClientWrapper : uses

    %% Existing event args composition
    DeviceAvailableEventArgs --> DeviceEndpointClient : contains
    AssetAvailableEventArgs --> AssetClient : contains
    AssetAvailableEventArgs --> DeviceEndpointClient : contains

    %% Existing client dependencies
    AssetClient --> IAzureDeviceRegistryClientWrapper : uses
    AssetClient --> AssetRuntimeHealthReporter : uses
    AssetClient --> ConnectorWorker : delegates to
    DeviceEndpointClient --> IAzureDeviceRegistryClientWrapper : uses

    %% New: AssetClient management action methods
    AssetClient --> SchemaRegistryClient : uses (new)
    AssetClient ..> ManagementActionExecutor : creates per action
    AssetClient ..> ManagementActionNotification : produces

    %% Executor chain
    ManagementActionExecutor --> CommandExecutor~TReq, TResp~ : wraps
    ManagementActionExecutor ..> ManagementActionRequest : produces

    %% Request/Response
    ManagementActionRequest ..> ManagementActionResponse : consumed by CompleteAsync
    ManagementActionResponse --> ManagementActionApplicationError : optional

    %% Notification hierarchy
    ManagementActionNotification <|-- ManagementActionUpdated
    ManagementActionNotification <|-- ManagementActionUpdatedWithNewExecutor
    ManagementActionNotification <|-- ManagementActionAssetUpdated
    ManagementActionNotification <|-- ManagementActionDeleted
    ManagementActionUpdatedWithNewExecutor --> ManagementActionExecutor : carries

    %% Style: new types
    style ManagementActionExecutor fill:#d4edda,stroke:#28a745
    style ManagementActionRequest fill:#d4edda,stroke:#28a745
    style ManagementActionResponse fill:#d4edda,stroke:#28a745
    style ManagementActionApplicationError fill:#d4edda,stroke:#28a745
    style ManagementActionNotification fill:#d4edda,stroke:#28a745
    style ManagementActionUpdated fill:#d4edda,stroke:#28a745
    style ManagementActionUpdatedWithNewExecutor fill:#d4edda,stroke:#28a745
    style ManagementActionAssetUpdated fill:#d4edda,stroke:#28a745
    style ManagementActionDeleted fill:#d4edda,stroke:#28a745
```

### Detailed: Full Class Members

New types are marked with `<<new>>`. Existing types shown for context.

```mermaid
classDiagram
    direction TB

    %% ============================================================
    %% LAYER: Microsoft.Extensions.Hosting
    %% ============================================================
    class BackgroundService {
        <<abstract>>
        +ExecuteAsync(CancellationToken) Task*
    }

    %% ============================================================
    %% LAYER: Azure.Iot.Operations.Protocol
    %% ============================================================
    class CommandExecutor~TReq, TResp~ {
        <<abstract>>
        +OnCommandReceived : Func
        +RequestTopicPattern : string
        +TopicTokenMap : Dictionary~string, string~
        +ServiceGroupId : string
        +ExecutionTimeout : TimeSpan
        +StartAsync() Task
        +StopAsync() Task
        +DisposeAsync() ValueTask
    }

    %% ============================================================
    %% LAYER: Azure.Iot.Operations.Services
    %% ============================================================
    class IAzureDeviceRegistryClientWrapper {
        <<interface>>
        +AssetChanged : EventHandler~AssetChangedEventArgs~
        +DeviceChanged : EventHandler~DeviceChangedEventArgs~
        +ObserveDevices()
        +ObserveAssets(deviceName, endpoint)
        +GetAssetStatusAsync() Task~AssetStatus~
        +UpdateAssetStatusAsync() Task~AssetStatus~
        +ReportManagementActionRuntimeHealthAsync() Task
    }

    class SchemaRegistryClient {
        +PutAsync(content, format, type, ...) Task~Schema~
        +GetAsync(schemaId, version) Task~Schema~
    }

    class AssetRuntimeHealthReporter {
        +ReportManagementActionHealthStatusAsync() Task
        +PauseReportingManagementActionAsync() Task
    }

    %% ============================================================
    %% LAYER: Azure.Iot.Operations.Connector (EXISTING)
    %% ============================================================
    class ConnectorBackgroundService {
        <<abstract>>
        +RunConnectorAsync(CancellationToken) Task*
    }
    BackgroundService <|-- ConnectorBackgroundService

    class ConnectorWorker {
        +WhileDeviceIsAvailable : Func~DeviceAvailableEventArgs, CancellationToken, Task~?
        +WhileAssetIsAvailable : Func~AssetAvailableEventArgs, CancellationToken, Task~?
        -_adrClient : IAzureDeviceRegistryClientWrapper
        -_mqttClient : IMqttClient
        -_applicationContext : ApplicationContext
        -_deviceTasks : ConcurrentDictionary
        -_assetTasks : ConcurrentDictionary
        #ExecuteAsync(CancellationToken) Task
        -OnAssetChanged(sender, AssetChangedEventArgs)
    }
    ConnectorBackgroundService <|-- ConnectorWorker

    class PollingTelemetryConnectorWorker {
        -_datasetSamplerFactory : IDatasetSamplerFactory
    }
    ConnectorWorker <|-- PollingTelemetryConnectorWorker

    class DeviceAvailableEventArgs {
        +DeviceName : string
        +Device : Device
        +InboundEndpointName : string
        +LeaderElectionClient : ILeaderElectionClient?
        +DeviceEndpointClient : DeviceEndpointClient
    }

    class AssetAvailableEventArgs {
        +DeviceName : string
        +Device : Device
        +InboundEndpointName : string
        +AssetName : string
        +Asset : Asset
        +LeaderElectionClient : ILeaderElectionClient?
        +AssetClient : AssetClient
        +DeviceEndpointClient : DeviceEndpointClient
    }

    class AssetClient {
        <<modified>>
        -_adrClient : IAzureDeviceRegistryClientWrapper
        -_connector : ConnectorWorker
        -_healthReporter : AssetRuntimeHealthReporter
        -_managementActionChannels* : Dictionary
        +GetAndUpdateAssetStatusAsync() Task~AssetStatus~
        +ForwardSampledDatasetAsync() Task
        +ForwardReceivedEventAsync() Task
        +ReportManagementActionRuntimeHealthAsync() Task
        +GetManagementActionExecutorAsync(group, action)* Task~ManagementActionExecutor?~
        +RecvManagementActionNotificationAsync(group, action)* Task~ManagementActionNotification~
        +ReportManagementActionRequestMessageSchemaAsync(group, action, schema)* Task
        +ReportManagementActionResponseMessageSchemaAsync(group, action, schema)* Task
        +DisposeAsync() ValueTask
    }

    class DeviceEndpointClient {
        -_adrClient : IAzureDeviceRegistryClientWrapper
        -_healthReporter : DeviceEndpointRuntimeHealthReporter
        +GetAndUpdateDeviceStatusAsync() Task~DeviceStatus~
        +ReportRuntimeHealthAsync() Task
        +DisposeAsync() ValueTask
    }

    %% ============================================================
    %% LAYER: Azure.Iot.Operations.Connector (NEW)
    %% ============================================================
    class ManagementActionExecutor {
        <<new>>
        -_commandExecutor : CommandExecutor
        +RecvRequestAsync(CancellationToken) Task~ManagementActionRequest?~
        +DisposeAsync() ValueTask
    }

    class ManagementActionRequest {
        <<new>>
        +Payload : ReadOnlySequence~byte~
        +ContentType : string
        +FormatIndicator : FormatIndicator
        +CustomUserData : Dictionary~string, string~
        +Timestamp : HybridLogicalClock?
        +InvokerId : string?
        +TopicTokens : Dictionary~string, string~
        +IsCancelled : bool
        +CompleteAsync(ManagementActionResponse, ct) Task
        +DisposeAsync() ValueTask
    }

    class ManagementActionResponse {
        <<new>>
        <<record>>
        +Payload : ReadOnlySequence~byte~
        +ContentType : string
        +CloudEvent : CloudEvent?
        +FormatIndicator : FormatIndicator
        +CustomUserData : Dictionary~string, string~?
        +ApplicationError : ManagementActionApplicationError?
    }

    class ManagementActionApplicationError {
        <<new>>
        <<record>>
        +ErrorCode : string
        +ErrorPayload : string
    }

    class ManagementActionNotification {
        <<new>>
        <<abstract record>>
    }
    class ManagementActionUpdated {
        <<new>>
        +Error : ConfigError?
    }
    class ManagementActionUpdatedWithNewExecutor {
        <<new>>
        +NewExecutor : ManagementActionExecutor?
        +Error : ConfigError?
    }
    class ManagementActionAssetUpdated {
        <<new>>
        +Error : ConfigError?
    }
    class ManagementActionDeleted {
        <<new>>
    }
    ManagementActionNotification <|-- ManagementActionUpdated
    ManagementActionNotification <|-- ManagementActionUpdatedWithNewExecutor
    ManagementActionNotification <|-- ManagementActionAssetUpdated
    ManagementActionNotification <|-- ManagementActionDeleted

    %% ============================================================
    %% RELATIONSHIPS: Existing
    %% ============================================================
    ConnectorWorker --> IAzureDeviceRegistryClientWrapper : _adrClient
    ConnectorWorker ..> DeviceAvailableEventArgs : creates
    ConnectorWorker ..> AssetAvailableEventArgs : creates
    AssetAvailableEventArgs --> AssetClient : contains
    AssetAvailableEventArgs --> DeviceEndpointClient : contains
    DeviceAvailableEventArgs --> DeviceEndpointClient : contains
    AssetClient --> IAzureDeviceRegistryClientWrapper : _adrClient
    AssetClient --> ConnectorWorker : _connector
    AssetClient --> AssetRuntimeHealthReporter : _healthReporter
    DeviceEndpointClient --> IAzureDeviceRegistryClientWrapper : _adrClient

    %% ============================================================
    %% RELATIONSHIPS: New
    %% ============================================================
    AssetClient --> SchemaRegistryClient : _schemaRegistryClient (new)
    AssetClient ..> ManagementActionExecutor : creates per action
    AssetClient ..> ManagementActionNotification : produces
    ManagementActionExecutor --> CommandExecutor~TReq, TResp~ : wraps
    ManagementActionExecutor ..> ManagementActionRequest : produces
    ManagementActionRequest ..> ManagementActionResponse : consumed by CompleteAsync
    ManagementActionResponse --> ManagementActionApplicationError : optional
    ManagementActionUpdatedWithNewExecutor --> ManagementActionExecutor : carries new executor

    %% ============================================================
    %% STYLE: Highlight new/modified types
    %% ============================================================
    style AssetClient fill:#fff3cd,stroke:#ffc107
    style ManagementActionExecutor fill:#d4edda,stroke:#28a745
    style ManagementActionRequest fill:#d4edda,stroke:#28a745
    style ManagementActionResponse fill:#d4edda,stroke:#28a745
    style ManagementActionApplicationError fill:#d4edda,stroke:#28a745
    style ManagementActionNotification fill:#d4edda,stroke:#28a745
    style ManagementActionUpdated fill:#d4edda,stroke:#28a745
    style ManagementActionUpdatedWithNewExecutor fill:#d4edda,stroke:#28a745
    style ManagementActionAssetUpdated fill:#d4edda,stroke:#28a745
    style ManagementActionDeleted fill:#d4edda,stroke:#28a745
```

**Legend:** Green = new types. Yellow = modified existing types. Members marked with `*` are new additions.

---

## Architecture Overview

The management action execution pipeline spans three existing .NET layers. New management action methods are added directly to the existing `AssetClient` (which already handles health reporting). No new callback surface on `ConnectorWorker` is needed — management action executors and notifications are accessed through `AssetClient` within the existing `WhileAssetIsAvailable` callback.

```
┌─────────────────────────────────────────────────────────────┐
│  User Code (connector implementation)                       │
│  WhileAssetIsAvailable callback (existing)                  │
│    - receives AssetAvailableEventArgs (contains AssetClient) │
│    - processes mgmt action requests via AssetClient           │
│    - reports schemas via AssetClient                          │
│    - runs until CancellationToken fires (deleted/shutdown)   │
└────────────────────────┬────────────────────────────────────┘
                         │ uses
┌────────────────────────▼────────────────────────────────────┐
│  Azure.Iot.Operations.Connector                             │
│                                                             │
│  New types:                                                 │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ ManagementActionExecutor                             │    │
│  │  - RecvRequestAsync() -> ManagementActionRequest?    │    │
│  │  - wraps CommandExecutor<byte[], byte[]>              │    │
│  └─────────────────────────────────────────────────────┘    │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ ManagementActionRequest                              │    │
│  │  - CompleteAsync(ManagementActionResponse)            │    │
│  │  - IsCancelled, Payload, ContentType, etc.           │    │
│  └─────────────────────────────────────────────────────┘    │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ ManagementActionResponse (record)                    │    │
│  │  - required Payload, ContentType, CloudEvent         │    │
│  │  - optional CustomUserData, FormatIndicator          │    │
│  │  - optional ApplicationError                         │    │
│  └─────────────────────────────────────────────────────┘    │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ ManagementActionApplicationError (record)            │    │
│  │  - ErrorCode, ErrorPayload                           │    │
│  └─────────────────────────────────────────────────────┘    │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ ManagementActionNotification (enum/union)            │    │
│  │  - Updated, UpdatedWithNewExecutor,                  │    │
│  │    AssetUpdated, Deleted                             │    │
│  └─────────────────────────────────────────────────────┘    │
│                                                             │
│  Modified types:                                            │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ AssetClient (extended)                               │    │
│  │  + GetManagementActionExecutorAsync()                │    │
│  │  + RecvManagementActionNotificationAsync()           │    │
│  │  + ReportManagementActionRequestMessageSchemaAsync() │    │
│  │  + ReportManagementActionResponseMessageSchemaAsync()│    │
│  │  + _managementActionChannels (internal state)        │    │
│  │  + _schemaRegistryClient (new dependency)            │    │
│  └─────────────────────────────────────────────────────┘    │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ ConnectorWorker (minor changes)                      │    │
│  │  ~ Injects SchemaRegistryClient into AssetClient     │    │
│  │  ~ Pushes mgmt action notifications to AssetClient   │    │
│  └─────────────────────────────────────────────────────┘    │
└────────────────────────┬────────────────────────────────────┘
                         │ depends on
┌────────────────────────▼────────────────────────────────────┐
│  Azure.Iot.Operations.Services                              │
│                                                             │
│  Existing types used (no changes expected):                 │
│  - IAzureDeviceRegistryClient / AzureDeviceRegistryClient   │
│  - SchemaRegistryClient (PutAsync for schema registration)  │
│  - AssetRuntimeHealthReporter (management action health)    │
│  - AssetStatus, AssetManagementGroupStatus,                 │
│    AssetManagementGroupActionStatus                         │
│  - MessageSchemaReference                                   │
│  - ManagementActionRuntimeHealthEventTelemetrySender        │
└────────────────────────┬────────────────────────────────────┘
                         │ depends on
┌────────────────────────▼────────────────────────────────────┐
│  Azure.Iot.Operations.Protocol                              │
│                                                             │
│  Existing types used (no changes expected):                 │
│  - CommandExecutor<TReq, TResp> (RPC executor base)         │
│  - ExtendedRequest<T>, ExtendedResponse<T>                  │
│  - CommandResponseMetadata                                  │
│  - ApplicationContext, IMqttPubSubClient                    │
└────────────────────────┬────────────────────────────────────┘
                         │ depends on
┌────────────────────────▼────────────────────────────────────┐
│  Azure.Iot.Operations.Mqtt                                  │
│  - IMqttClient (MQTT connection)                            │
└─────────────────────────────────────────────────────────────┘
```

### System Context: Connector and External Dependencies

Black-box view of a Connector application built on `Azure.Iot.Operations.Connector` and the external systems it communicates with. Captures the object network at deployment scope, without internal class structure. The two AIO services have no special wire protocol of their own — they are reached via in-process proxy clients (`IAzureDeviceRegistryClient`, `SchemaRegistryClient`) that issue RPC over MQTT, so those proxies are shown explicitly to make the object network match reality.

```mermaid
flowchart LR
    subgraph Edge["Edge / k8s pod"]
        subgraph App["Connector application (Azure.Iot.Operations.Connector)"]
            Core["Connector core<br/>(ConnectorWorker, AssetClient,<br/>ManagementActionExecutor, etc.)"]
            ADRProxy["IAzureDeviceRegistryClient<br/>(in-process proxy)"]
            SRProxy["SchemaRegistryClient<br/>(in-process proxy)"]
            MqttClient["IMqttClient / IMqttPubSubClient"]
        end
        MQTT["MQTT Broker<br/>(AIO broker)"]
    end

    subgraph Services["AIO services (remote, reached via MQTT RPC)"]
        ADR["ADR Service<br/>(Asset & Device Registry)"]
        SRS["Schema Registry Service"]
    end

    subgraph CloudSide["Cloud / control plane"]
        Invoker["Management-action Invoker<br/>(portal, automation, operator)"]
    end

    Device["Southbound device or system<br/>(opaque to the SDK)"]

    %% In-process: connector core uses the two proxies + the MQTT client.
    Core -- "AssetChanged events,<br/>Get/UpdateAssetStatusAsync,<br/>Report*RuntimeHealthAsync" --> ADRProxy
    Core -- "PutAsync / GetAsync" --> SRProxy
    Core -- "ManagementAction RPC<br/>(via CommandExecutor)" --> MqttClient

    %% All three in-process clients funnel through the same MQTT client / broker.
    ADRProxy -- "RPC + telemetry over MQTT" --> MqttClient
    SRProxy -- "RPC over MQTT" --> MqttClient
    MqttClient -- "publish/subscribe" --> MQTT

    %% Northbound: services consume the MQTT messages the proxies publish.
    MQTT <-- "AssetChanged / DeviceChanged,<br/>Get/UpdateAssetStatus,<br/>Report*RuntimeHealth" --> ADR
    MQTT <-- "Schema Put / Get" --> SRS

    %% Cloud-side caller publishes management-action RPC to the same broker.
    Invoker <-- "ManagementAction RPC<br/>(request/response)" --> MQTT

    %% Southbound: device interaction is connector-specific (HTTP, Modbus, OPC UA, custom TCP, etc.).
    Core <-- "device-protocol-specific<br/>(sample datasets, receive events,<br/>execute management actions)" --> Device

    style Core fill:#fff3cd,stroke:#ffc107
    style ADRProxy fill:#fff3cd,stroke:#ffc107
    style SRProxy fill:#fff3cd,stroke:#ffc107
    style MqttClient fill:#fff3cd,stroke:#ffc107
    style MQTT fill:#e2e3e5,stroke:#6c757d
    style ADR fill:#e2e3e5,stroke:#6c757d
    style SRS fill:#e2e3e5,stroke:#6c757d
    style Invoker fill:#e2e3e5,stroke:#6c757d
    style Device fill:#e2e3e5,stroke:#6c757d
```

**What flows where:**

| Edge | Direction | Purpose |
|---|---|---|
| Connector core ↔ `IAzureDeviceRegistryClient` | in-process | Proxy for ADR. Subscribes to `AssetChanged` / `DeviceChanged`, exposes `GetAssetStatusAsync` / `UpdateAssetStatusAsync`, and forwards `Report*RuntimeHealth` telemetry. Owned by `ConnectorWorker`; injected into `AssetClient` and `DeviceEndpointClient`. |
| Connector core ↔ `SchemaRegistryClient` | in-process | Proxy for Schema Registry. Exposes `PutAsync` / `GetAsync`. Used by `AssetClient` during `ReportManagementAction{Request,Response}MessageSchemaAsync`. |
| Both proxies ↔ `IMqttClient` | in-process | Both proxies (and the management-action `CommandExecutor`) share the single MQTT client owned by `ConnectorWorker`. |
| MQTT Broker ↔ ADR Service | over MQTT | Materializes the proxy calls as RPC and telemetry on AIO topic conventions. |
| MQTT Broker ↔ Schema Registry Service | over MQTT | Materializes `PutAsync` / `GetAsync` as RPC on AIO topic conventions. |
| Invoker ↔ MQTT | bidirectional | Cloud-side caller publishes management-action RPC requests and receives responses. The connector responds via the same broker. |
| Connector core ↔ Device | bidirectional | Out of scope of the SDK. Each connector implements its own southbound protocol (HTTP polling, Modbus, OPC UA, custom TCP, etc.) and translates it into dataset samples, received events, and management-action invocations. |

**Object-network correspondence:** the in-process proxy boxes are exactly the dependencies declared in the `External Dependencies` table — `IAzureDeviceRegistryClient` and `SchemaRegistryClient` (Azure.Iot.Operations.Services), both running on top of `IMqttPubSubClient` (Azure.Iot.Operations.Protocol). The proxies are co-located with the connector in the same process; ADR and Schema Registry are out-of-process AIO services reached only via MQTT. The southbound `Device` edge has no SDK type — it is whatever the connector author plugs in.

---

## New Types to Introduce

All new types live in **Azure.Iot.Operations.Connector** (the top-level, user-facing layer).

### 1. ManagementActionExecutor

Wraps `CommandExecutor<TReq, TResp>` from the Protocol layer to receive management action RPC requests over MQTT.

```
Dependencies:
  - ApplicationContext (from ConnectorWorker)
  - IMqttPubSubClient (from ConnectorWorker)
  - MQTT topic derived from management action definition
  - Command name: "{managementGroupName}::{actionName}"
```

**Responsibilities:**
- Subscribe to the management action's MQTT request topic
- Receive incoming RPC requests as `ManagementActionRequest`
- Handle graceful shutdown (drain remaining requests)

**Serialization:** Use `CommandExecutor<byte[], byte[]>` with the existing
`PassthroughSerializer` (found in `Services/StateStore/Generated/Common/`
and `samples/Protocol/TestEnvoys/`). Bytes pass through unchanged in both
directions. `ContentType` and `FormatIndicator` flow via
`CommandRequestMetadata` / `CommandResponseMetadata`, not via the
serializer, so the serializer's hardcoded `application/octet-stream`
default is harmless — it is overridden by the metadata objects. No new
`BypassPayload` type is required on the .NET side. See Open Question #1
for full resolution.

### 2. ManagementActionRequest

Represents an incoming management action invocation. Created internally by `ManagementActionExecutor`.

```
Properties (read-only):
  - ReadOnlySequence<byte> Payload  // non-contiguous-friendly; .ToArray() if a byte[] is needed
  - string ContentType
  - FormatIndicator FormatIndicator
  - Dictionary<string, string> CustomUserData
  - HybridLogicalClock? Timestamp
  - string? InvokerId
  - Dictionary<string, string> TopicTokens
  - bool IsCancelled

Methods:
  - Task CompleteAsync(ManagementActionResponse response, CancellationToken ct)
```

**Auto-error on dispose:** If `CompleteAsync` is never called, disposal should send an error response back (matching Rust's Drop impl).

### 3. ManagementActionResponse (record)

```csharp
public record ManagementActionResponse
{
    public required ReadOnlySequence<byte> Payload { get; set; }
    public required string ContentType { get; set; }
    public required CloudEvent? CloudEvent { get; set; }
    public FormatIndicator FormatIndicator { get; set; } = FormatIndicator.UnspecifiedBytes;
    public Dictionary<string, string>? CustomUserData { get; set; }
    public ManagementActionApplicationError? ApplicationError { get; set; }
}
```

### 4. ManagementActionApplicationError (record)

```csharp
public record ManagementActionApplicationError
{
    public required string ErrorCode { get; set; }
    public string ErrorPayload { get; set; } = string.Empty;
}
```

### 5. ManagementActionNotification

C# doesn't have Rust-style enums. Options:
- Abstract base class + derived types (pattern matching via `switch` on type)
- Single class with a `NotificationType` enum + nullable `Executor` property

```csharp
// Option: discriminated union via base class
public abstract record ManagementActionNotification;

public record ManagementActionUpdated(ConfigError? Error) : ManagementActionNotification;
public record ManagementActionUpdatedWithNewExecutor(
    ManagementActionExecutor? NewExecutor, 
    ConfigError? Error) : ManagementActionNotification;
public record ManagementActionAssetUpdated(ConfigError? Error) : ManagementActionNotification;
public record ManagementActionDeleted : ManagementActionNotification;
```

### 6. New Methods on AssetClient (Modifications)

`AssetClient` gains management action methods. Internally, it maintains per-action state (notification channels, cached executors) keyed by `"{managementGroupName}::{managementActionName}"`.

```
New dependencies (injected internally by ConnectorWorker):
  - SchemaRegistryClient (for schema registration)

New internal state:
  - _managementActionChannels : ConcurrentDictionary<string, Channel<ManagementActionNotification>>
  - _schemaRegistryClient : SchemaRegistryClient

New methods (public):
  - Task<ManagementActionExecutor?> GetManagementActionExecutorAsync(
        string managementGroupName, string managementActionName, CancellationToken ct)
        // Returns null if no valid executor exists right now (e.g. the current definition
        // was rejected with a ConfigError). Callers should await
        // RecvManagementActionNotificationAsync for the next definition and retry.
  - Task<ManagementActionNotification> RecvManagementActionNotificationAsync(
        string managementGroupName, string managementActionName, CancellationToken ct)
  - Task ReportManagementActionRequestMessageSchemaAsync(
        string managementGroupName, string managementActionName,
        ConnectorMessageSchema schema, CancellationToken ct)
  - Task ReportManagementActionResponseMessageSchemaAsync(
        string managementGroupName, string managementActionName,
        ConnectorMessageSchema schema, CancellationToken ct)
  - Task ReportManagementActionRequestMessageSchemaReferenceAsync(
        string managementGroupName, string managementActionName,
        MessageSchemaReference schemaRef, CancellationToken ct)
  - Task ReportManagementActionResponseMessageSchemaReferenceAsync(
        string managementGroupName, string managementActionName,
        MessageSchemaReference schemaRef, CancellationToken ct)
```

**Notification delivery:** Internally, `AssetClient` uses `Channel<ManagementActionNotification>` per action. `ConnectorWorker` pushes notifications when ADR raises `AssetChanged` events. The user reads via `RecvManagementActionNotificationAsync()`. `Writer.Complete()` signals deletion.

**Why on AssetClient:** Review feedback — keeping all asset concerns in one place avoids a nested `ManagementActionClient` within `AssetClient`. The user already has `AssetClient` from `AssetAvailableEventArgs` in the `WhileAssetIsAvailable` callback; management action methods are a natural extension of it. The `managementGroupName` + `managementActionName` parameters serve as the action identifier (replacing the per-action client's implicit identity).

---

## Modifications to Existing Types

### AssetClient

See section 6 above for full details. Summary of additions:

```
New dependency:
  + SchemaRegistryClient _schemaRegistryClient

New internal state:
  + ConcurrentDictionary<string, Channel<ManagementActionNotification>> _managementActionChannels

New public methods:
  + GetManagementActionExecutorAsync(groupName, actionName) -> ManagementActionExecutor?
  + RecvManagementActionNotificationAsync(groupName, actionName) -> ManagementActionNotification
  + ReportManagementActionRequestMessageSchemaAsync(groupName, actionName, schema)
  + ReportManagementActionResponseMessageSchemaAsync(groupName, actionName, schema)
  + ReportManagementActionRequestMessageSchemaReferenceAsync(groupName, actionName, ref)
  + ReportManagementActionResponseMessageSchemaReferenceAsync(groupName, actionName, ref)

New internal methods (called by ConnectorWorker):
  + PushManagementActionNotification(groupName, actionName, notification)
  + InitManagementActionChannel(groupName, actionName)
```

### ConnectorWorker

```
Modified methods:
  ~ AssetClient construction:
    Now also injects SchemaRegistryClient into AssetClient.

  ~ OnAssetChanged / AssetAvailable:
    When an asset becomes available (Created/Updated), iterate
    Asset.ManagementGroups[].Actions[] and initialize notification
    channels on the AssetClient. On definition changes, push
    appropriate notifications (Updated, UpdatedWithNewExecutor,
    Deleted) to AssetClient's internal channels.

  ~ AssetUnavailableAsync:
    Complete all management action notification channels on the
    AssetClient being removed (signals deletion to user code).
```

**Key design choice:** Management action lifecycle is scoped to asset lifetime. When an asset is deleted, all its management action notification channels are completed, causing `RecvManagementActionNotificationAsync` to return `ManagementActionDeleted`. When a management action definition changes within an asset update, `ConnectorWorker` pushes the appropriate notification type (Updated or UpdatedWithNewExecutor) to the corresponding channel on `AssetClient`.

---

## External Dependencies

No new NuGet packages required. All dependencies are already present:

| Dependency | Package | Used For |
|---|---|---|
| `CommandExecutor<TReq, TResp>` | Azure.Iot.Operations.Protocol | RPC executor base for receiving requests |
| `SchemaRegistryClient` | Azure.Iot.Operations.Services | Registering request/response schemas |
| `IAzureDeviceRegistryClient` | Azure.Iot.Operations.Services | Asset status get/update for schema refs |
| `AssetRuntimeHealthReporter` | Azure.Iot.Operations.Services | Health reporting (already used) |
| `IMqttPubSubClient` | Azure.Iot.Operations.Protocol | MQTT communication |
| `MQTTnet` | (transitive) | MQTT transport |

---

## Data Flow

### 1. Startup: Management Action Discovery

```mermaid
sequenceDiagram
    participant ADR as ADR Service
    participant CW as ConnectorWorker
    participant AC as AssetClient
    participant MAE as ManagementActionExecutor
    participant CE as CommandExecutor
    participant MQTT as MQTT Broker
    participant User as User Callback

    ADR->>CW: AssetChanged (Created)
    activate CW
    CW->>CW: Parse Asset.ManagementGroups and Actions
    CW->>AC: new AssetClient(..., schemaRegistryClient)
    loop For each management action
        CW->>AC: InitManagementActionChannel(groupName, actionName)
        CW->>CE: new CommandExecutor(topic, commandName)
        CE->>MQTT: Subscribe to action request topic
        CW->>MAE: new ManagementActionExecutor(commandExecutor)
        CW->>AC: Store initial executor for action
    end
    CW->>User: WhileAssetIsAvailable(assetEventArgs, ct)
    activate User
    Note over User: assetEventArgs.AssetClient has mgmt action methods
    User->>AC: GetManagementActionExecutorAsync(group, action)
    AC->>User: Returns ManagementActionExecutor
    User->>User: Enter select-style loop per action
    deactivate CW
```

### 2. Inbound: Management Action Request/Response

```mermaid
sequenceDiagram
    participant Invoker as Invoker (Cloud/Service)
    participant MQTT as MQTT Broker
    participant CE as CommandExecutor
    participant MAE as ManagementActionExecutor
    participant User as User Callback

    Invoker->>MQTT: Publish RPC request
    MQTT->>CE: Deliver message on subscribed topic
    CE->>CE: Parse headers, validate request
    CE->>MAE: OnCommandReceived(ExtendedRequest)
    MAE->>MAE: Wrap as ManagementActionRequest
    MAE->>User: RecvRequestAsync() returns request

    alt Success
        User->>User: Process request (read/write/call on device)
        User->>User: Build ManagementActionResponse record
        User->>MAE: request.CompleteAsync(response)
        MAE->>CE: Return ExtendedResponse
        CE->>MQTT: Publish RPC response
        MQTT->>Invoker: Deliver response
    else Application Error
        User->>User: Build response with ApplicationError
        User->>MAE: request.CompleteAsync(errorResponse)
        MAE->>CE: Return ExtendedResponse.WithApplicationError()
        CE->>MQTT: Publish error response
        MQTT->>Invoker: Deliver error response
    else Request dropped (not completed)
        MAE->>CE: Auto-send error response on dispose
        CE->>MQTT: Publish error response
        MQTT->>Invoker: Deliver error response
    end
```

### 3. Schema Registration

```mermaid
sequenceDiagram
    participant User as User Callback
    participant AC as AssetClient
    participant SR as SchemaRegistryClient
    participant MQTT as MQTT Broker
    participant SRS as Schema Registry Service
    participant ADR as ADR Service

    User->>AC: ReportManagementActionRequestMessageSchemaAsync(group, action, schema)
    activate AC

    AC->>SR: PutAsync(content, format, type, version)
    SR->>MQTT: Publish schema registration RPC
    MQTT->>SRS: Deliver to Schema Registry
    SRS->>MQTT: Return Schema (with ID)
    MQTT->>SR: Deliver response
    SR->>AC: Return Schema object

    AC->>AC: Build MessageSchemaReference

    AC->>ADR: UpdateAssetStatusAsync(assetStatus)
    Note over AC,ADR: Sets RequestMessageSchemaReference on action status

    ADR-->>AC: Updated AssetStatus
    AC->>User: Return
    deactivate AC

    Note over User: Same flow for Response schema
```

### 4. Lifecycle: Definition Update (Same Topic)

```mermaid
sequenceDiagram
    participant ADR as ADR Service
    participant CW as ConnectorWorker
    participant AC as AssetClient
    participant HR as AssetRuntimeHealthReporter
    participant User as User Callback

    ADR->>CW: AssetChanged (Updated)
    activate CW
    CW->>CW: Diff old vs new management actions
    CW->>CW: Action exists, topic unchanged

    CW->>AC: PushManagementActionNotification(Updated)
    deactivate CW
    AC->>User: RecvManagementActionNotificationAsync() returns ManagementActionUpdated

    User->>AC: PauseManagementActionRuntimeHealthReportingAsync(group, action)
    AC->>HR: PauseReportingManagementActionAsync(group, action)
    Note over AC,HR: Sets cached entry to null. The periodic sender skips null entries, and a later report call overwrites it. There is no separate resume API.
    User->>User: Re-validate definition
    User->>User: Update internal state
    User->>AC: GetAndUpdateAssetStatusAsync(handler)
    activate AC
    AC->>ADR: GetAssetStatusAsync()
    ADR-->>AC: current AssetStatus
    AC->>AC: handler(current) writes AssetManagementGroupActionStatus.Error
    AC->>ADR: UpdateAssetStatusAsync(desired)
    ADR-->>AC: persisted AssetStatus
    AC-->>User: persisted AssetStatus
    deactivate AC
    Note over User,ADR: Persistence happens in ADR. AssetClient is stateless — it brokers a get-modify-update against the ADR service.
    User->>User: Re-report schemas (required on any update)
    User->>AC: ReportManagementActionRuntimeHealthAsync(new status)
    AC->>HR: ReportManagementActionHealthStatusAsync(events)
    HR->>ADR: ReportManagementActionRuntimeHealthAsync(events)
    Note over User,ADR: Two channels are updated on every definition change. Durable config status (above) is persisted via UpdateAssetStatusAsync. Volatile runtime health (this call) is telemetry through AssetRuntimeHealthReporter and also ends the pause. See section 8 for the reporter's dedup and periodic re-send behavior.
    User->>User: Continue processing with same executor
```

### 5. Lifecycle: Definition Update (New Topic - New Executor)

```mermaid
sequenceDiagram
    participant ADR as ADR Service
    participant CW as ConnectorWorker
    participant AC as AssetClient
    participant HR as AssetRuntimeHealthReporter
    participant MAE_old as Old Executor
    participant MAE_new as New Executor
    participant CE as CommandExecutor
    participant MQTT as MQTT Broker
    participant User as User Callback

    ADR->>CW: AssetChanged (Updated)
    activate CW
    CW->>CW: Diff: action topic changed

    CW->>CE: new CommandExecutor(newTopic, commandName)
    CE->>MQTT: Subscribe to new request topic
    CW->>MAE_new: new ManagementActionExecutor(commandExecutor)

    CW->>AC: PushManagementActionNotification(UpdatedWithNewExecutor)
    deactivate CW
    AC->>User: RecvManagementActionNotificationAsync() returns UpdatedWithNewExecutor

    User->>AC: PauseManagementActionRuntimeHealthReportingAsync(group, action)
    AC->>HR: PauseReportingManagementActionAsync(group, action)
    Note over AC,HR: Same pause-via-null-cache mechanism as section 4.

    User->>User: Drain old executor
    loop Until old executor returns null
        User->>MAE_old: RecvRequestAsync()
        MAE_old->>User: Stale request
        User->>MAE_old: request.CompleteAsync(errorResponse)
        Note over User,MAE_old: "ManagementActionDefinitionOutdated"
    end
    User->>MAE_old: Dispose

    User->>User: Re-report schemas (required on any update)
    User->>User: Switch to new executor
    User->>AC: GetAndUpdateAssetStatusAsync(handler)
    activate AC
    AC->>ADR: GetAssetStatusAsync()
    ADR-->>AC: current AssetStatus
    AC->>AC: handler(current) writes AssetManagementGroupActionStatus.Error
    AC->>ADR: UpdateAssetStatusAsync(desired)
    ADR-->>AC: persisted AssetStatus
    AC-->>User: persisted AssetStatus
    deactivate AC
    Note over User,ADR: Persistence happens in ADR (same get-modify-update pattern as section 4).
    User->>AC: ReportManagementActionRuntimeHealthAsync(new status)
    AC->>HR: ReportManagementActionHealthStatusAsync(events)
    HR->>ADR: ReportManagementActionRuntimeHealthAsync(events)
    User->>MAE_new: RecvRequestAsync() (continue loop)
```

### 6. Lifecycle: Management Action Deleted

```mermaid
sequenceDiagram
    participant ADR as ADR Service
    participant CW as ConnectorWorker
    participant AC as AssetClient
    participant MAE as ManagementActionExecutor
    participant CE as CommandExecutor
    participant MQTT as MQTT Broker
    participant User as User Callback

    ADR->>CW: AssetChanged (Updated, action removed)
    activate CW
    CW->>CW: Diff: action no longer in definition
    CW->>AC: PushManagementActionNotification(Deleted)
    CW->>AC: Complete notification channel for this action
    deactivate CW

    AC->>User: RecvManagementActionNotificationAsync() returns ManagementActionDeleted

    User->>User: Drain remaining requests
    loop Until executor returns null
        User->>MAE: RecvRequestAsync()
        MAE->>User: Queued request
        User->>MAE: request.CompleteAsync(errorResponse)
        Note over User,MAE: "ManagementActionDeleted"
    end

    User->>MAE: Dispose
    MAE->>CE: StopAsync()
    CE->>MQTT: Unsubscribe from topic

    User->>User: Stop processing this action
```

### 7. Lifecycle: Asset Deleted (Cascading Cleanup)

```mermaid
sequenceDiagram
    participant ADR as ADR Service
    participant CW as ConnectorWorker
    participant AC as AssetClient
    participant User as User Callback

    ADR->>CW: AssetChanged (Deleted)
    activate CW

    CW->>AC: Complete all management action notification channels

    CW->>CW: Cancel CancellationToken for WhileAssetIsAvailable

    User->>User: OperationCanceledException / channels complete
    User->>User: Dispose all ManagementActionExecutors
    User->>User: Clean up action processing state

    CW->>CW: Await WhileAssetIsAvailable task
    CW->>AC: Dispose AssetClient
    deactivate CW
```

### 8. Health Reporting (Existing — No Changes)

```mermaid
sequenceDiagram
    participant User as User Callback
    participant AC as AssetClient
    participant HR as AssetRuntimeHealthReporter
    participant ADR as ADR Service
    participant MQTT as MQTT Broker

    User->>AC: ReportManagementActionRuntimeHealthAsync
    AC->>HR: ReportManagementActionHealthStatusAsync(...)
    HR->>HR: Dedup check against cached health

    alt Health changed
        HR->>MQTT: Send ManagementActionRuntimeHealthEventTelemetry
        MQTT->>ADR: Deliver health telemetry
    else Health unchanged
        HR->>HR: Skip (deduplicated)
    end

    Note over HR: Background periodic re-reporting also runs on interval
```

---

## Open Questions

1. **BypassPayload / raw passthrough in CommandExecutor:** Does `CommandExecutor<TReq, TResp>` support a raw byte[] passthrough mode (no serialization), or do we need a no-op serializer? The Rust side uses `BypassPayload` for this.
   - **RESOLVED: Already supported.** Use `CommandExecutor<byte[], byte[]>` with the existing `PassthroughSerializer` (found in `Services/StateStore/Generated/Common/` and `samples/Protocol/TestEnvoys/`). Bytes pass through unchanged in both directions. Content-Type and Format Indicator metadata flow through `CommandRequestMetadata` / `CommandResponseMetadata` separately from the serializer, so the serializer's hardcoded `application/octet-stream` default is harmless — it gets overridden by the metadata objects. The user reads content-type from `ManagementActionRequest.ContentType` and sets it on `ManagementActionResponse.ContentType`.

2. **Topic extraction:** How to derive the MQTT request topic from `AssetManagementGroupAction.Topic` / `AssetManagementGroup.DefaultTopic`? The Rust side has `try_executor_topic_from_management_topics()`. Need to understand the topic format and how it maps to `CommandExecutor`'s `RequestTopicPattern`.
   - **RESOLVED: Dynamic topic configuration at runtime.** The topic comes from the asset definition: `AssetManagementGroupAction.Topic` with fallback to `AssetManagementGroup.DefaultTopic` (fallback not yet implemented in Rust, but should be in .NET). The `CommandExecutor` supports setting `RequestTopicPattern` at construction time without the `[CommandTopic]` attribute — so `ManagementActionExecutor` creates a `CommandExecutor<byte[], byte[]>` and configures topic properties dynamically. `TopicTokenMap` is populated with known values (device name, endpoint, etc.), and unresolved tokens become `+` (MQTT wildcard). The executor subscribes to `$share/{ServiceGroupId}/{resolved topic}`.

3. **Notification channel internals:** `AssetClient.RecvManagementActionNotificationAsync()` needs an internal delivery mechanism. `Channel<T>` is the natural choice but has no codebase precedent. Alternative: `SemaphoreSlim` + queue, or `TaskCompletionSource` chain.
   - **Decision: `Channel<ManagementActionNotification>` (Option A).**
   - `ConnectorWorker` pushes notifications via `_channel.Writer.TryWrite()`, user consumes via `_channel.Reader.ReadAsync()` inside `RecvManagementActionNotificationAsync()`. `Writer.Complete()` signals end-of-life (deletion). Internal-only — the user never sees the channel, only the `RecvManagementActionNotificationAsync()` method. Each management action gets its own channel, keyed by `"{groupName}::{actionName}"` in `AssetClient._managementActionChannels`.
   - Alternatives considered:
     - `TaskCompletionSource` chain: simpler but fragile — doesn't handle queuing if multiple notifications arrive before user reads; needs manual synchronization; easy to get wrong.
     - `SemaphoreSlim` + `ConcurrentQueue<T>`: uses patterns already in the codebase (`SemaphoreSlim` is in `AssetClient`), but reinvents `Channel<T>` with more code and more edge cases (disposal, completion signaling).
   - Rationale: `Channel<T>` is a standard BCL type (`System.Threading.Channels`) purpose-built for async producer-consumer. It handles queuing, cancellation, completion, and thread safety out of the box. No new NuGet dependency. The lack of codebase precedent is a weak objection — it's internal-only plumbing.

4. **Health reporting ownership:** Health reporting currently lives on `AssetClient`. With management action functionality now also on `AssetClient`, the user naturally has access to `ReportManagementActionRuntimeHealthAsync()` alongside the new management action methods.
   - **RESOLVED: No change needed.** The user already has `AssetClient` from `AssetAvailableEventArgs` in the `WhileAssetIsAvailable` callback. Since management action execution methods are now on `AssetClient` too, the user has unified access to both health reporting and action execution from the same object. No separate event args or client needed.

5. **Concurrency model for updates:** When `AssetChanged` fires with updated management actions, `ConnectorWorker` needs to diff old vs. new action definitions to determine which actions were added/removed/updated. This diffing logic needs to be defined.
   - **DEFERRED.** Requires decisions on: where to cache old asset definitions, what fields to compare (topic only vs full definition), and how to handle non-topic changes. Will resolve during implementation.
