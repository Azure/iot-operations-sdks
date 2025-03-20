# Azure IoT Operations Go SDK

| [Samples](samples) | [Release Notes](https://github.com/Azure/iot-operations-sdks/releases?q=go%2F) |

This directory contains the source code, samples, and tests for the Azure IoT Operations Go SDK.

Review the following links for more information about the product and broader developer experience:

-   Azure IoT Operations [Learn documentation](https://learn.microsoft.com/azure/iot-operations/)
-   Azure IoT Operations [SDKs overview documentation](/doc)

## Overview

The following Azure IoT Operations modules are available:

| Module                                                           | Description                                                            |
| ---------------------------------------------------------------- | ---------------------------------------------------------------------- |
| [**github.com/Azure/iot-operations-sdks/go/mqtt**](mqtt)         | MQTTv5 client library for decoupled asynchronous applications          |
| [**github.com/Azure/iot-operations-sdks/go/protocol**](protocol) | Utilities for using the Azure IoT Operations Protocol (RPC, Telemetry) |
| [**github.com/Azure/iot-operations-sdks/go/services**](services) | Clients for using services of Azure IoT Operations                     |

> [!CAUTION]
> These modules are currently in preview and are subject to change until version 1.0. Pinning a specific release will protect you from any breaking changes, which are subject to occur until we release 1.0.

To install a modules, add them to the `go.mod` for your project:

```bash
go get github.com/Azure/iot-operations-sdks/go/mqtt@<version>
go get github.com/Azure/iot-operations-sdks/go/protocol@<version>
go get github.com/Azure/iot-operations-sdks/go/services@<version>
```

## Getting Started

1. Complete the [setup](/doc/setup.md) steps to setup your cluster and install Azure IoT Operations.

2. Install [Go](https://go.dev/doc/install).

3. Run one of the Go [samples](samples).

