# Azure IoT Operations .NET SDK

This folder contains the source code, tests, and sample code for the **Azure IoT Operations .NET SDK**.

Review the following links for more information about the product and broader developer experience:

* Azure IoT Operations [Learn documentation](https://learn.microsoft.com/azure/iot-operations/)
* Azure IoT Operations [SDKs overview documentation](/doc)

## Getting started

To get started with a .NET SDK tutorial, follow these steps:

1. To set up your cluster and install Azure IoT Operations, refer to the [setup guide](/doc/setup.md).

1. Install the [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

1. Run the [Event Driven Application](samples/applications/EventDrivenApp) tutorial.

## Developing your application

1. Create a new .NET application. Using one of the [existing samples](samples) is a good starting point.

1. Install the packages into your project:

    ```bash
    dotnet add package Azure.IoT.Operations.Mqtt
    dotnet add package Azure.IoT.Operations.Protocol
    dotnet add package Azure.IoT.Operations.Services
    ```

1. Read the [documentation](#packages) for details on using the various SDK components and debugging your application.

## Developing your Akri connector

The Akri Connector package (this service is currently in private preview) is located in our ADO package feed:

1. Add our [package feed](https://dev.azure.com/azure-iot-sdks/iot-operations/_artifacts/feed/preview):

    ```bash
    dotnet nuget add source -n AzureIoTOperations https://pkgs.dev.azure.com/azure-iot-sdks/iot-operations/_packaging/preview/nuget/v3/index.json
    ```

1. Install the packages into your project:

    ```bash
    dotnet add package Azure.IoT.Operations.Connector
    dotnet add package Azure.IoT.Operations.Mqtt
    dotnet add package Azure.IoT.Operations.Protocol
    dotnet add package Azure.IoT.Operations.Services
    ```

## Deploying your application

Refer to the [Deploy an edge application](/doc/edge_application/deploy.md) document to build your image and deploy it to your cluster for final validation.

## Packages

The following Azure IoT Operations packages are available:

| Name | API | Package | Description |
|-|-|-|-|
| [**Mqtt**](src/Azure.Iot.Operations.Mqtt) | [:link:](https://azure.github.io/iot-operations-sdks/dotnet/api/Azure.Iot.Operations.Mqtt.html) | `Azure.Iot.Operations.Mqtt` | MQTT5 fundamentals such as session management including connections and authentication |
| [**Protocol**](src/Azure.Iot.Operations.Protocol) | [:link:](https://azure.github.io/iot-operations-sdks/dotnet/api/Azure.Iot.Operations.Protocol.html) | `Azure.Iot.Operations.Protocol` | Protocol implementations built on MQTT5 such as telemetry and RPC |
| [**Services**](src/Azure.Iot.Operations.Services) | [:link:](https://azure.github.io/iot-operations-sdks/dotnet/api/Azure.Iot.Operations.Services.html) | `Azure.Iot.Operations.Services` | Integrate with IoT Operations services such as state store, lease lock, leader election and schema registry |

> [!CAUTION]
> The prerelease versions (`--prerelease`) should be used as they are the official preview builds. Omitting this flag will default to the daily builds, which may contain unpredictable behavior and undocumented breaking changes.

## Samples

Refer to the [samples directory](samples) for a comprehensive list of samples using the Azure IoT Operations .NET SDK.
