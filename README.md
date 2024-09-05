# Azure IoT Operation SDKs

## What is Azure IoT Operations?

*Azure IoT Operations* is a unified data plane for the edge. It's composed of a set of modular, scalable, and highly available data services that run on Azure Arc-enabled edge Kubernetes clusters. It enables data capture from various different systems and integrates with data modeling applications such as Microsoft Fabric to help organizations deploy the industrial metaverse.

See [the Azure IoT Operations Learn](https://learn.microsoft.com/azure/iot-operations/) documentation to learn more about the product as well as detailed instructions on deployment and configuration.

## Why use an SDK?

The *Azure IoT Operations SDKs* are a suite of libraries across many languages designed to assist in the development of applications for Azure IoT Operations.

The focus of the SDKs it to assist customers in developing applications by providing the following features:

| Feature | Description |
|-|-|
| **Highly available** | Provides infrastructure and guidance to build HA into your applications |
| **Any language** | The SDKs target multiple languages to support any development environment |
| **Secure** | Uses the latest crypto libraries and protocols |
| **Zero data loss** | Builds on MQTT broker to remove data loss due to application failure |
| **Low latency** | Optimized layering and tight MQTT client coupling minimized overheads |
| **Integration with IoT Operations services** | Libraries provide access to services such as state store |
| **Simplify complex messaging** | Provide support for communication between applications via MQTT5 using an RPC implementation |
| **Support** | The SDKs are maintained and supported by a dedicated team at Microsoft |

## Getting started

This repository supports [GitHub Codespaces](https://github.com/features/codespaces). It provides a quick wayto get started with evaluationing the SDKs by creating a test K3d cluster to install IoT Operations in.

1. Create a Codespace with this repository:

   [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Azure/iot-operations-sdks)

1. Following the remaining [Quickstart](https://learn.microsoft.com/azure/iot-operations/get-started-end-to-end-sample/quickstart-deploy) to get IoT Operations up and running.

1. Reconfigure IoT Operations ready for running samples and quickstarts:

   ```bash
   tools/deployment/deploy-aio.sh release
   ```

1. Run one of the quickstarts:
    
    1. [go](./go)

## SDK Components

### Component status

:green_circle: Supported  
:yellow_circle: In progress  
:orange_circle: Planned  
:red_circle: No plans

### Language libraries

The repository contains the following libraries with the associated language support:

| Component | Description | [.NET](./dotnet) | [Go](./go) | [Rust](./rust) |
|-|-|-|-|-|
| **Session** client | Creates the underlying MQTT client, authenticates against MQTT Broker and maintains the connection. | :green_circle: | :green_circle: | :green_circle: |
| **State store** client | Client that enables interaction with the state store and provides the ability to get/set/delete and watch a key | :green_circle: | :orange_circle: | :orange_circle: |
| **Lease lock** client | Create a lock for a shared resource | :green_circle: | :orange_circle: | :orange_circle: |
| **Leader election** client | Assigns the elected application (leader) when multiple applications a deployed in a highly available configuration | :green_circle: | :orange_circle: | :orange_circle: |
| **Schema registry** client | Interact with the schema registry | :green_circle: | :orange_circle: | :orange_circle: |
| **RPC** protocol | RPC (request/response) protocol build on top of MQTT5 | :green_circle: | :green_circle: | :green_circle: |
| **Telemetry** protocol | Telemetry (publish) protocol build on top of MQTT5 | :green_circle: | :green_circle: | :orange_circle: |

### Protocol compiler

The Protocol compiler is a command line tool distributed as a NuGet package. It generates client libraries and server stubs in multiple languages.

| Component | Description | [.NET](./dotnet) | [Go](./go) | [Rust](./rust) |
|-|-|-|-|-|
| **Protocol compiler** | The Protocol Compiler generates client libraries and server stubs from a DTDL definition. | :green_circle: | :yellow_circle:  | :yellow_circle:  |
| [**JSON**](https://www.json.org/) Serialization | Json serialization support | :green_circle: | :green_circle: | :green_circle: |
| [**Apache Avro**](https://avro.apache.org/) Serialization | Avro serialization support | :orange_circle: | :orange_circle: | :orange_circle: |
| [**Protobuf**](https://protobuf.dev/) Serialization | Protobuf serialzation support| :orange_circle: | :orange_circle: | :orange_circle: |

### Additional tools

Other tools available for use during development of IoT Operation applications.

| Tool | Description |
|-|-|
| **State store CLI** | Interact with the state store via a CLI. Get, set and delete keys. |

## Using the SDK

Each language provides getting started and samples for using the SDK:

* [.NET](./dotnet)
* [Go](./go)
* [Rust](./rust)

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
