# Samples and Tutorials

## Setup

Refer to the [Getting Started](/README.md#getting-started) for setting up your development environment **prior** to running the tutorials and samples.

The following is a list of tutorials and samples are available across all languages. Each language may have additional samples which can also be found within each language directory.

## Tutorials

The tutorials listed below are step-by-step instructions to deploy a fully functioning application to a cluster and observer the functioning output.

| Tutorial | Description | Go | .NET | Rust |
|-|-|-|-|-|
| Event Driven Application | Read from a topic and perform a sliding window calculation, utilizing the State Store to cache historical data. The result is written to a second topic. | Go | [.NET](/dotnet/tutorials/EventDrivenApp) | Rust |
| Rest Akri Connector | | Go | .NET | Rust |
| SQL Akri Connector | | Go | .NET | Rust |

## Samples

### MQTT

|Category | Sample | Description | Go | .NET | Rust |
|-|-|-|-|-|-|
| MQTT | Session client - SAT auth | Connect to the MQTT broker using SAT auth |
|| Session client - x509 auth | Connect to the MQTT broker using x509 auth | 
||
| Protocol | Telemetry | |
|| Telemetry with Cloud Events | |
|| Command | |
||
| Services | State store client |
|| State store client - observe key |
|| Lease lock client |
|| Leader election client |
|| Schema registry client |
|| ADR client |
|| Akri client |
||
| Codegen | Telemetry and command |
|| Telemetry with primitive schema |
|| Telemetry with complex schema |
|| Command variants |

## Languages Samples

Refer to each language folder for additional samples:

**.NET SDK** - 
* [.NET samples](/dotnet/samples)

**Go SDK**
* [Go samples](/go/samples)

**Rust SDK**
* [Rust Protocol samples](/rust/azure_iot_operations_protocol/examples/)
* [Rust MQTT samples](/rust/azure_iot_operations_mqtt/examples/)
* [Rust Services samples](/rust/azure_iot_operations_services/examples/)
