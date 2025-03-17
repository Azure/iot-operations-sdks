# Samples and tutorials

> [!CAUTION]
>
> The samples and tutorials provided in this repository are for **demonstration purposed only**, and should not be deployed to a production system or included within an application without a full understanding of how they operate.
>
> Additionally some samples may use a username and password for authentication. This is used for sample simplicity and a production system should use a robust authentication mechanism such as certificates.

The following is a list of tutorials and samples that are available across all languages. Each language may have additional samples which can also be found within each language directory.

> [!TIP]
> Refer to the [getting started](/README.md#getting-started) documentation for setting up your development environment **prior** to running the samples and tutorials.

A :yellow_circle: mean the tutorial or sample is planned in the near future.

## Tutorials

The tutorials listed below are step-by-step instructions to deploy a fully functioning application to a cluster and observer the functioning output.

| Tutorial | Description | Go | .NET | Rust |
|-|-|:-:|:-:|:-:|
| Event Driven Application | Read from a topic and perform a sliding window calculation, utilizing the State Store to cache historical data. The result is written to a second topic. | :yellow_circle: | [.NET](/dotnet/tutorials/EventDrivenApp) | :yellow_circle: |

## Samples

|Category | Sample | Description | Go | .NET | Rust |
|-|-|-|:-:|:-:|:-:|
| **MQTT** | **Session client** | Connect to the MQTT broker | :yellow_circle: | [.NET](/dotnet/samples/Mqtt/SessionClient) | [Rust](https://github.com/Azure/iot-operations-sdks/blob/main/rust/azure_iot_operations_mqtt/examples/simple_sample.rs) |
|| **Session client - SAT auth** | Connect to the MQTT broker with SAT | :yellow_circle: | :yellow_circle: | [Rust](https://github.com/Azure/iot-operations-sdks/blob/main/rust/azure_iot_operations_mqtt/examples/sat_auth_sample.rs) |
|| **Session client - x509 auth** | Connect to the MQTT broker with x509 | :yellow_circle: | :yellow_circle: | :yellow_circle: |
||
| **Protocol** | **Telemetry client** | Send and receive messages to a MQTT topic | :yellow_circle: | :yellow_circle: | [Sender](https://github.com/Azure/iot-operations-sdks/blob/main/rust/azure_iot_operations_protocol/examples/simple_telemetry_sender_sample.rs)</br>[Receiver](https://github.com/Azure/iot-operations-sdks/blob/main/rust/azure_iot_operations_protocol/examples/simple_telemetry_receiver_sample.rs) |
|| **Telemetry client with Cloud Events** | Send and receive messages to a MQTT topic with cloud events | [Go](/go/samples/protocol/cloudevents) | :yellow_circle: | [Sender](https://github.com/Azure/iot-operations-sdks/blob/main/rust/azure_iot_operations_protocol/examples/simple_telemetry_sender_sample.rs)</br>[Receiver](https://github.com/Azure/iot-operations-sdks/blob/main/rust/azure_iot_operations_protocol/examples/simple_telemetry_receiver_sample.rs) |
|| **Command client** | Invoke and execute and command using the MQTT RPC protocol | :yellow_circle: | :yellow_circle: | [Invoker](https://github.com/Azure/iot-operations-sdks/blob/main/rust/azure_iot_operations_protocol/examples/simple_rpc_invoker_sample.rs)</br>[Executor](https://github.com/Azure/iot-operations-sdks/blob/main/rust/azure_iot_operations_protocol/examples/simple_rpc_executor_sample.rs) |
||
| **Services** | **State store client** | Get, set and delete a key | [Go](https://github.com/Azure/iot-operations-sdks/tree/main/go/samples/statestore) | [.NET](/dotnet/samples/Services/StateStoreClient) | [Rust](https://github.com/Azure/iot-operations-sdks/blob/main/rust/azure_iot_operations_services/examples/state_store_client.rs) |
|| **State store client - observe key** | Observe a key and receive a notification | :yellow_circle: | [.NET](/dotnet/samples/Services/StateStoreObserveKey) | [Rust](https://github.com/Azure/iot-operations-sdks/blob/main/rust/azure_iot_operations_services/examples/state_store_client.rs) |
|| **Leased lock client** | Lock a key in the state store shared between applications | [Go](https://github.com/Azure/iot-operations-sdks/tree/main/go/samples/leasedlock) | [.NET](/dotnet/samples/Services/LeasedLockClient) | :yellow_circle: |
|| **Leader election client** | Leader assignment for highly available applications | :yellow_circle: | [.NET](/dotnet/samples/Services/PassiveReplication) | :yellow_circle: |
|| **Schema registry client** | Get and set schemas from the registry | [Go](/go/samples/services/schemaregistry) | [.NET](/dotnet/samples/Services/SchemaRegistryClient) | [Rust](/rust/azure_iot_operations_services/examples/schema_registry_client.rs) |
|| **Akri client** | Notify Akri services of discovered assets | :yellow_circle: | :yellow_circle: | :yellow_circle: |
||
| **Codegen** | **Telemetry & command** | A basic telemetry and command | :yellow_circle: | :yellow_circle: | :yellow_circle: |
|| **Telemetry + primitive schema** | Telemetry using primitive types such as integers, bool and float | :yellow_circle: | :yellow_circle: | :yellow_circle: |
|| **Telemetry + complex schema** | Telemetry using complex types such as maps and objects | :yellow_circle: | :yellow_circle: | :yellow_circle: |
|| **Command variants** | Commands using idempotent and cacheable | :yellow_circle: | :yellow_circle: | :yellow_circle: |

## Additional samples and tutorials

Refer to each language directory below for additional samples.

**.NET SDK:**

* [.NET samples](/dotnet/samples)
* [.NET tutorials](/dotnet/tutorials)

**Go SDK:**

* [Go samples](/go/samples)

**Rust SDK:**

* [Rust Protocol samples](/rust/azure_iot_operations_protocol/examples/)
* [Rust MQTT samples](/rust/azure_iot_operations_mqtt/examples/)
* [Rust Services samples](/rust/azure_iot_operations_services/examples/)
