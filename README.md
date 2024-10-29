# Azure IoT Operation SDKs

[![CI-dotnet](https://github.com/Azure/iot-operations-sdks/actions/workflows/ci-dotnet.yml/badge.svg)](https://github.com/Azure/iot-operations-sdks/actions/workflows/ci-dotnet.yml)
[![CI-go](https://github.com/Azure/iot-operations-sdks/actions/workflows/ci-go.yml/badge.svg)](https://github.com/Azure/iot-operations-sdks/actions/workflows/ci-go.yml)
[![CI-Rust](https://github.com/Azure/iot-operations-sdks/actions/workflows/ci-rust.yml/badge.svg)](https://github.com/Azure/iot-operations-sdks/actions/workflows/ci-rust.yml)
[![e2e-cross-language-samples](https://github.com/Azure/iot-operations-sdks/actions/workflows/e2e-cross-language-samples.yml/badge.svg)](https://github.com/Azure/iot-operations-sdks/actions/workflows/e2e-cross-language-samples.yml)

> [!CAUTION]
> The assets in this repository are currently in Private Preview and have been made available for early access and feedback purposes.

## What is Azure IoT Operations?

*Azure IoT Operations* is a unified data plane for the edge. It's composed of a set of modular, scalable, and highly available data services that run on Azure Arc-enabled edge Kubernetes clusters. It enables data capture from various different systems and integrates with data modeling applications such as Microsoft Fabric to help organizations deploy the industrial metaverse.

See [the Azure IoT Operations Learn](https://learn.microsoft.com/azure/iot-operations/) documentation to learn more about the product as well as detailed instructions on deployment and configuration.

## Why use an SDK?

The *Azure IoT Operations SDKs* are a suite of tools and libraries across multiple languages designed to aid the development of applications for Azure IoT Operations.

The focus of the SDKs it to assist customers in developing applications by providing the following features:

| Feature | Description |
|-|-|
| <code>**Highly available** | Provides infrastructure and guidance to build HA into your applications |
| **Any language** | The SDKs target multiple languages to support any development environment |
| **Secure** | Uses the latest crypto libraries and protocols |
| **Zero data loss** | Builds on MQTT broker to remove data loss due to application failure |
| **Low latency** | Optimized layering and tight MQTT client coupling minimized overheads |
| **Integration with IoT Operations services** | Libraries provide access to services such as state store |
| **Simplify complex messaging** | Provide support for communication between applications via MQTT5 using an RPC implementation |
| **Support** | The SDKs are maintained and supported by a dedicated team at Microsoft |

## Getting started

Use [GitHub Codespaces](https://github.com/features/codespaces) to try the Azure IoT Operations SDKs on a Kubernetes cluster without installing anything on your local machine.

> [!NOTE] 
> For alternative platforms and more in-depth setup instruction, refer to the [environment setup](/doc/setup.md) document.

1. Create a **codespace**, and enter your Azure details to store them as environment variables:

   [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Azure/iot-operations-sdks?hide_repo_select=true&editor=vscode)

   > :stop_sign:<code style="color:red">**Important**</code>  
   >  
   > Open the codespace in VS Code Desktop (**Ctrl + Shift + P > Codespaces: Open in VS Code Desktop**).  This is required to login to Azure in a later step.

1. Follow the [Learn docs](https://learn.microsoft.com/azure/iot-operations/get-started-end-to-end-sample/quickstart-deploy?tabs=codespaces) to connect your cluster to Azure Arc and deploy Azure IoT Operations.

1. Configure Azure IoT Operations for SDK development:

   ```bash
   ./tools/deployment/deploy-aio.sh release
   ```

1. Run through a [tutorial or sample](/samples) to get started with developing!

## SDK Features

| State | Support | Location |
|-|-|-|
| :green_circle:&nbsp;Complete | Feature is released and actively supported by the team. | [Releases](https://github.com/Azure/iot-operations-sdks/releases) |
| :yellow_circle:&nbsp;In&nbsp;progress | Under development, no support provided. | `main` and `feature` branches |
| :orange_circle:&nbsp;Planned | Refer to [discussions](https://github.com/Azure/iot-operations-sdks/discussions) for details on planned features. | - |
| :red_circle:&nbsp;Not&nbsp;planned | Refer to [discussions](https://github.com/Azure/iot-operations-sdks/discussions) for detail on unplanned features. | - |

### Feature status

The following features are available or planned, along with the current language support:

| Feature | Description | [.NET](./dotnet) | [Go](./go) | [Rust](./rust) |
|-|-|-|-|-|
| **Session** client | Creates the underlying MQTT client, authenticates against MQTT Broker and maintains the connection. | :green_circle: | :green_circle: | :green_circle: |
| **RPC** protocol | RPC (request/response) protocol build on top of MQTT5 | :green_circle: | :green_circle: | :green_circle: |
| **Telemetry** protocol | Telemetry (publish) protocol build on top of MQTT5 | :green_circle: | :green_circle: | :green_circle: |
| **State store** client | Client that enables interaction with the state store and provides the ability to get/set/delete and watch a key | :green_circle: | :green_circle: | :green_circle: |
| **Lease lock** client | Create a lock for a shared resource | :green_circle: | :green_circle: | :yellow_circle: |
| **Leader election** client | Assigns the elected application (leader) when multiple applications a deployed in a highly available configuration | :green_circle: | :yellow_circle: | :yellow_circle: |
| **Schema registry** client | Interact with the schema registry to fetch and store asset schemas | :green_circle: | :orange_circle: | :orange_circle: |
| **ADR** client | Configuration for the MQTT Broker and asset endpoint | :yellow_circle: | :orange_circle: | :orange_circle: |
| **Akri** client | Record discovered assets and asset endpoints | :yellow_circle: | :orange_circle: | :orange_circle: |

### Protocol compiler

The Protocol compiler is a command line tool distributed as a NuGet package. It generates client libraries and server stubs in multiple languages.

| Component | Description | [.NET](/dotnet) | [Go](/go) | [Rust](/rust) |
|-|-|-|-|-|
| [**Protocol compiler CLI**](/codegen) | The Protocol Compiler generates client libraries and server stubs from a DTDL definition. | :green_circle: | :green_circle:  | :green_circle:  |
| [**JSON**](https://www.json.org/) Serialization | Json serialization support | :green_circle: | :green_circle:  | :green_circle:  |
| [**Apache Avro**](https://avro.apache.org/) Serialization | Avro serialization support | :green_circle: | :orange_circle: | :orange_circle: |
| [**Protobuf**](https://protobuf.dev/) Serialization | Protobuf serialization support| :orange_circle: | :orange_circle: | :orange_circle: |

### Other tooling

Tools available for use during development of IoT Operation applications.

| Tool | Description | Status |
|-|-|-|
| [**State store CLI**](/tools/dsscli) | Interact with the state store via a CLI. Get, set and delete keys. | :green_circle: |

## Need help?

* Read through the [SDK documentation](./doc)
* Check for an answer in the [troubleshooting](./doc/troubleshooting.md)
* File an issue via [Github Issues](https://github.com/Azure/iot-operations-sdks/issues/new/choose)
* Check the [discussions](https://github.com/Azure/iot-operations-sdks/discussions) or start a new one

## Contributing

This project welcomes contributions and suggestions. Most contributions require you to agree to a
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
