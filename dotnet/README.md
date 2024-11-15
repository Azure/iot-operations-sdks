# Azure IoT Operations .NET SDK

This folder contains the source code, tests, and sample code for the **Azure IoT Operations .NET SDK**. 

Review the following links for more information about the product and broader developer experience:

* Azure IoT Operations [Learn documentation](https://learn.microsoft.com/azure/iot-operations/)
* Azure IoT Operations [SDKs overview documentation](/doc)

## Getting started

To get started with a .NET SDK tutorial, follow these steps:
 
1. Complete the [Getting started](/README.md#getting-started) steps to setup your cluster and install Azure IoT Operations.

1. Install the [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

1. Run the [Event Driven Application](tutorials/EventDrivenApp) tutorial.

## Developing an edge application

1. Create a new .NET application. Use one of the [existing samples](samples) as a starting point if desired.

1. Add our [NuGet package feed](https://dev.azure.com/azure-iot-sdks/iot-operations/_artifacts/feed/preview):

    ```bash
    dotnet nuget add source https://pkgs.dev.azure.com/azure-iot-sdks/iot-operations/_packaging/preview/nuget/v3/index.json -n AzureIoTOperations
    ```

1. Install the NuGet packages into your project:

    ```bash
    dotnet add package Azure.IoT.Operations.Mqtt --prerelease
    dotnet add package Azure.IoT.Operations.Protocol --prerelease
    dotnet add package Azure.IoT.Operations.Services --prerelease
    ```

1. Add your own logic by reviewing the [package documentation](#packages) for details on using the various SDK components and debug your application locally.

1. [Deploy the application](/doc/deploy.md) to your cluster for final validation.

## Packages

The following Azure IoT Operations packages are available:

| Name | Package | Description |
|-|-|-|
| [**Protocol**](src/Azure.Iot.Operations.Protocol) | `Azure.Iot.Operations.Protocol` | Protocol implementations built on MQTT5 such as telemetry and RPC |
| [**Mqtt**](src/Azure.Iot.Operations.Mqtt) | `Azure.Iot.Operations.Mqtt` | MQTT5 fundamentals such as session management including connections and authentication |
| [**Services**](src/Azure.Iot.Operations.Services) | `Azure.Iot.Operations.Services` | Integrate with IoT Operations services such as state store, lease lock, leader election and schema registry |

> [!CAUTION]
> The prerelease versions (`--prerelease`) should be used as they are the official preview builds. Omitting this flag will default to the daily builds, which may contain unpredictable behavior and undocumented breaking changes.
 
## Samples

Refer to the [samples](samples) directory for a comprehensive list of samples using the .NET SDK.
