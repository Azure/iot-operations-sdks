# Samples and tutorials

> [!CAUTION]
>
> The samples and tutorials provided in this repository are for **demonstration purposed only**, and should not be deployed to a production system or included within an application without a full understanding of how they operate.
>
> Additionally some samples may use a username and password for authentication. This is used for sample simplicity and a production system should use a robust authentication mechanism such as certificates.

The following is a list of tutorials and samples that are available across all languages. Each language may have additional samples which can also be found within each language directory.

> [!TIP]
> Refer to the [setup documentation](/doc/setup.md) for setting up your development environment **prior** to running the samples and tutorials.

A :yellow_circle: mean the tutorial or sample is planned.

## Tutorials

The tutorials listed below are step-by-step instructions to deploy a fully functioning application to a cluster and observer the functioning output.

| Tutorial | Description | Go | .NET | Rust |
|-|-|:-:|:-:|:-:|
| Event Driven Application | Read from a topic and perform a sliding window calculation, utilizing the State Store to cache historical data. The result is written to a second topic. | :yellow_circle: | [.NET](/samples/event_driven_app) | [Rust](/samples/event_driven_app) |

## Samples

|Category | Sample | Description | Go | .NET | Rust |
|-|-|-|:-:|:-:|:-:|
| **MQTT** | **Session client** | Connect to the MQTT broker | :yellow_circle: | [.NET](/dotnet/samples/Mqtt/SessionClient) | [Rust](/rust/azure_iot_operations_mqtt/examples/simple_sample.rs) |
|| **Session client - SAT auth** | Connect to the MQTT broker with SAT | :yellow_circle: | :yellow_circle: | [Rust](/rust/azure_iot_operations_mqtt/examples/sat_auth.rs) |
|| **Session client - x509 auth** | Connect to the MQTT broker with x509 | :yellow_circle: | :yellow_circle: | :yellow_circle: |
||
| **Protocol** | **Telemetry client with Cloud Events** | Send and receive messages to a MQTT topic with cloud events | [Go](/go/samples/protocol/cloudevents) | [.NET](/dotnet/samples/Protocol/CloudEvents) | [Sender](/rust/azure_iot_operations_protocol/examples/simple_telemetry_sender.rs)</br>[Receiver](/rust/azure_iot_operations_protocol/examples/simple_telemetry_receiver.rs) |
|| **Command client** | Invoke and execute and command using the MQTT RPC protocol | :yellow_circle: | :yellow_circle: | [Invoker](/rust/azure_iot_operations_protocol/examples/simple_rpc_invoker.rs)</br>[Executor](/rust/azure_iot_operations_protocol/examples/simple_rpc_executor.rs) |
|| **RPC with shared subscription** | RPC executors using shared subscriptions | :yellow_circle: | :yellow_circle: | [Rust](/rust/azure_iot_operations_protocol/examples/rpc_executors_with_shared_subscription.rs) |
||
| **Services** | **State store client** | Get, set and delete a key | [Go](/go/samples/services/statestore) | [.NET](/dotnet/samples/Services/StateStoreClient) | [Rust](/rust/azure_iot_operations_services/examples/state_store_client.rs) |
|| **State store client - observe key** | Observe a key and receive a notification | [Go](/go/samples/services/statestore) | [.NET](/dotnet/samples/Services/StateStoreObserveKey) | [Rust](/rust/azure_iot_operations_services/examples/state_store_client.rs) |
|| **Leased lock client** | Lock a key in the state store shared between applications | [Go](/go/samples/services/leasedlock) | [.NET](/dotnet/samples/Services/LeasedLockClient) | [Rust](/rust/azure_iot_operations_services/examples/lock_client.rs) |
|| **Schema registry client** | Get and set schemas from the registry | [Go](/go/samples/services/schemaregistry) | [.NET](/dotnet/samples/Services/SchemaRegistryClient) | [Rust](/rust/azure_iot_operations_services/examples/schema_registry_client.rs) |
|| **ADR discovery** | Notify Akri services of discovered devices || :yellow_circle: | [Rust](/rust/azure_iot_operations_services/examples/adr_discovery.rs) |
|| **ADR operations** | Azure Device Registry device asset operations || :yellow_circle: | [Rust](/rust/azure_iot_operations_services/examples/adr_device_asset.rs) |
||
| **Connector** | **Polling-driven scaffolding** | Template for creating polling-driven connectors || [Dotnet](/dotnet/templates/PollingTelemetryConnector/) | [Rust](/rust/sample_applications/sample_connector_scaffolding) |
|| **Event-driven scaffolding** | Template for creating event-driven connectors || [Dotnet](/dotnet/templates/EventDrivenTelemetryConnector/) | :yellow_circle: |
||
| **Codegen*** | **Command** | A basic command client and server | [Client](/codegen/demo/go/cmdclient/)</br>[Server](/codegen/demo/go/cmdserver/) | [Client](/codegen/demo/dotnet/ProtocolCompiler.Demo/CmdClient/)</br>[Server](/codegen/demo/dotnet/ProtocolCompiler.Demo/CmdServer/) | [Client](/codegen/demo/rust/protocol_compiler_demo/cmd_client/)</br>[Server](/codegen/demo/rust/protocol_compiler_demo/cmd_server/) |
|| **Telemetry** | A basic telemetry client and server | [Client](/codegen/demo/go/telemclient/)</br>[Server](/codegen/demo/go/telemserver/) | [Client](/codegen/demo/dotnet/ProtocolCompiler.Demo/TelemClient/)</br>[Server](/codegen/demo/dotnet/ProtocolCompiler.Demo/TelemServer/) | [Client](/codegen/demo/rust/protocol_compiler_demo/telem_client/)</br>[Server](/codegen/demo/rust/protocol_compiler_demo/telem_server/) |
|| **Telemetry + primitive schema** | Telemetry using primitive types such as integers, bool and float | [Go](/codegen/test/samples/go/) | [.NET](/codegen/test/samples/dotnet/) | [Rust](/codegen/test/samples/rust/) |
|| **Telemetry + complex schema** | Telemetry using complex types such as maps and objects | [Go](/codegen/test/samples/go/) | [.NET](/codegen/test/samples/dotnet/) | [Rust](/codegen/test/samples/rust/) |
|| **Command variants** | Commands using idempotent and cacheable | [Go](/codegen/test/samples/go/) | [.NET](/codegen/test/samples/dotnet/) | [Rust](/codegen/test/samples/rust/) |

_* Codegen samples must be generated, the referenced samples above may not be present otherwise_

## Additional samples and tutorials

Refer to each language directory below for additional samples.

**.NET SDK:**

* [Tutorials](/dotnet/samples/applications)
* [Samples](/dotnet/samples)

**Go SDK:**

<!-- * [Tutorials](/go/samples/application) -->
* [Samples](/go/samples)

**Rust SDK:**

* [Tutorials](/rust/sample_applications)
* [Protocol samples](/rust/azure_iot_operations_protocol/examples/)
* [MQTT samples](/rust/azure_iot_operations_mqtt/examples/)
* [Services samples](/rust/azure_iot_operations_services/examples/)
* [Connector samples](/rust/azure_iot_operations_connector/examples/)
