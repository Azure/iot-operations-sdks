# Azure IoT Operations .NET SDK

This folder contains the source code, tests, and sample code for the **Azure IoT Operations .NET SDK**.

## Getting started

Follow the [Getting Started](./README.md#getting-started) guide to bring up an environment in CodeSpaces.

## Packaging

The following Azure IoT Operations packages are available:

| Name | Package | Description |
|-|-|-|
| [**Protocol**](../doc#protocol) | `Azure.Iot.Operations.Protocol` | MQTT fundamentals such as telemetry, RPC, connection settings |
| [**Services**](../doc#services) | `Azure.Iot.Operations.Services` | Integrate with IoT Operations services such as state store, lease lock, leader election and schema registry |
| [**Mqtt**](../doc#mqtt) | `Azure.Iot.Operations.Mqtt` | MQTT fundamentals such as telemetry, RPC, connection settings |

### Installing

> [!NOTE]
> The packages are currently marked `prerelease` as part of the SDK preview. These are considered more stable than the daily builds.

1. Add the [NuGet package feed](https://dev.azure.com/azure-iot-sdks/iot-operations/_artifacts/feed/preview):

    ```bash
    dotnet nuget add source https://pkgs.dev.azure.com/azure-iot-sdks/iot-operations/_packaging/preview/nuget/v3/index.json -n AzureIoTOperations
    ```

1. Install the package into your project:

    ```bash
    dotnet add package <PACKAGE_NAME> --prerelease
    ```