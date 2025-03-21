# Azure IoT Operations SDKs

[![CI-dotnet](https://github.com/Azure/iot-operations-sdks/actions/workflows/ci-dotnet.yml/badge.svg)](https://github.com/Azure/iot-operations-sdks/actions/workflows/ci-dotnet.yml)
[![CI-go](https://github.com/Azure/iot-operations-sdks/actions/workflows/ci-go.yml/badge.svg)](https://github.com/Azure/iot-operations-sdks/actions/workflows/ci-go.yml)
[![CI-Rust](https://github.com/Azure/iot-operations-sdks/actions/workflows/ci-rust.yml/badge.svg)](https://github.com/Azure/iot-operations-sdks/actions/workflows/ci-rust.yml)
[![e2e-cross-language-samples](https://github.com/Azure/iot-operations-sdks/actions/workflows/e2e-cross-language.yml/badge.svg)](https://github.com/Azure/iot-operations-sdks/actions/workflows/e2e-cross-language.yml)

> [!CAUTION]
> The assets in this repository are currently in **Public Preview** and have been made available for early access and feedback purposes.

## What is Azure IoT Operations?

*Azure IoT Operations* is a unified data plane for the edge. It's composed of a set of modular, scalable, and highly available data services that run on Azure Arc-enabled edge Kubernetes clusters. It enables data capture from various different systems and integrates with data modeling applications such as Microsoft Fabric to help organizations deploy the industrial metaverse.

See [the Azure IoT Operations Learn](https://learn.microsoft.com/azure/iot-operations/) documentation to learn more about the product as well as detailed instructions on deployment and configuration.

## Why use an SDK?

The *Azure IoT Operations SDKs* are a suite of tools and libraries across multiple languages designed to aid the development of applications for Azure IoT Operations.

The focus of the SDKs it to assist customers in developing applications by providing the following features:

| Feature | Description |
|-|-|
| **Highly available** | Provides infrastructure and guidance to build HA into your applications |
| **Choice of language** | The SDKs target multiple languages to support any development environment |
| **Secure** | Uses the latest crypto libraries and protocols |
| **Zero data loss** | Builds on MQTT broker to remove data loss due to application failure |
| **Low latency** | Optimized layering and tight MQTT client coupling minimized overheads |
| **Integration with IoT Operations services** | Libraries provide access to services such as state store |
| **Simplify complex messaging** | Provide support for communication between applications via MQTT5 using an RPC implementation |
| **Support** | The SDKs are maintained and supported by a dedicated team at Microsoft |

## Getting started

Use GitHub Codespaces to try the Azure IoT Operations SDKs on a Kubernetes cluster without installing anything on your local machine.

> [!TIP] 
> For additional platforms and more in-depth setup instruction, refer to the [setup documentation](/doc/setup.md).

1. Create a **codespace** from the *Azure IoT Operations SDKs* repository:

    [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Azure/iot-operations-sdks?quickstart=1&editor=vscode)

1. Follow the [Azure IoT Operations documentation](https://learn.microsoft.com/azure/iot-operations/get-started-end-to-end-sample/quickstart-deploy?tabs=codespaces#connect-cluster-to-azure-arc) to connect Azure Arc and deploy Azure IoT Operations.

1. Run the `deploy-aio` script to configure Azure IoT Operations for development:

    ```bash
    ./tools/deployment/deploy-aio.sh
    ```

## Next steps

* [Read the documentation](./doc) on building edge applications using the Azure IoT Operations SDKs

* Review the [API documentation](https://azure.github.io/iot-operations-sdks/) for the SDKs

* Refer to language directory for instructions on using each SDK:

    * **.NET** SDK - [/dotnet](/dotnet)
    * **Go** SDK - [/go](/go)
    * **Rust** SDK - [/rust](/rust)
  
* Take a look at the [samples and tutorials](/samples) for an summary of the different samples available across the languages.

## Components

The following tables outline the current components, along with the associated language support.

> [!TIP]
> Additional information on the SDK components is available in our [component documentation](doc/components.md).

| State | Support |
|-|-|
| :green_circle:&nbsp;Complete | Feature is released and **actively** supported by the team. |
| :yellow_circle:&nbsp;In&nbsp;progress | Under development, **no support** provided. |
| :orange_circle:&nbsp;Planned | Work is planned in the near future. |

| Feature | Description | .NET | Go | Rust |
|-|-|-|-|-|
| **Session** client | Creates the underlying MQTT client, authenticates against MQTT Broker and maintains the connection. | :green_circle: | :green_circle: | :green_circle: |
| **Command** client | Command (invoker/executor) client build on top of MQTT5 | :green_circle: | :green_circle: | :green_circle: |
| **Telemetry** client | Telemetry (sender/receiver) client build on top of MQTT5 | :green_circle: | :green_circle: | :green_circle: |
| **Schema registry** client | Interact with the schema registry to fetch and store asset schemas | :green_circle: | :green_circle: | :green_circle: |
| **State store** client | Client that enables interaction with the state store and provides the ability to get/set/delete and watch a key | :green_circle: | :green_circle: | :green_circle: |
| **Lease lock** client | Create a lock for a shared resource | :green_circle: | :green_circle: | :green_circle: |
| **Leader election** client | Assigns the elected application (leader) when multiple applications a deployed in a highly available configuration | :green_circle: | :yellow_circle: | :yellow_circle: |
| **Akri** client | Asset and asset endpoint configuration and asset discovery | :green_circle: | :yellow_circle: | :yellow_circle: |

## Need support?

Refer to [SUPPORT.md](./SUPPORT.md) for guidance on reporting bugs and getting assistance.

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
