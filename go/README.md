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

## Getting Started

1. Complete the [Getting started](/README.md#getting-started) steps to setup your cluster and install Azure IoT Operations.

2. Install [Go](https://go.dev/doc/install).

3. Run one of the Go [samples](samples).

> [!NOTE]
> The samples reference the libraries from this repository via `go.work` and can be run directly. However, due to this repository currently being private, the Go tooling must be given access to it in order to install these modules in external projects. Instructions on how to do so via SSH are provided below.

## Installing modules

1. Ensure you have access to GitHub via [SSH](https://docs.github.com/en/authentication/connecting-to-github-with-ssh).

2. Update your Git configuration with:

    ```bash
    git config --global url."git@github.com:Azure/iot-operations-sdks".insteadOf "https://github.com/Azure/iot-operations-sdks"
    ```

3. Ensure your `GOPRIVATE` environment variable includes `github.com/Azure/iot-operations-sdks` (simply set it to this value if it is not already present).

4. Take a dependency on the module(s) you want to use via:
    ```bash
    go get github.com/Azure/iot-operations-sdks/go/mqtt@<version>
    go get github.com/Azure/iot-operations-sdks/go/protocol@<version>
    go get github.com/Azure/iot-operations-sdks/go/services@<version>
    ```
