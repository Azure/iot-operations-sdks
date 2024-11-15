# Azure IoT Operations Go SDK

This directory contains the source code, samples, and tests for the Azure IoT
Operations Go SDK.

## Overview

The following Azure IoT Operations modules are available:

| Module                                                           | Description                                                            |
| ---------------------------------------------------------------- | ---------------------------------------------------------------------- |
| [**github.com/Azure/iot-operations-sdks/go/mqtt**](mqtt)         | MQTTv5 client library for decoupled asynchronous applications          |
| [**github.com/Azure/iot-operations-sdks/go/protocol**](protocol) | Utilities for using the Azure IoT Operations Protocol (RPC, Telemetry) |
| [**github.com/Azure/iot-operations-sdks/go/services**](services) | Clients for using services of Azure IoT Operations                     |

## Installing modules

> [!CAUTION]
> These modules are currently in preview and are subject to change until version
> 1.0. Pinning a specific release will protect you from any breaking changes,
> which are subject to occur until we release 1.0.

> [!NOTE]
> All but the last of the following steps are related to accessing the modules
> under the private GitHub repository and will not be necessary upon release.

1. Ensure you have access to GitHub via
   [SSH](https://docs.github.com/en/authentication/connecting-to-github-with-ssh).

2. Update your Git configruation with:

    ```bash
    git config --global url."git@github.com:Azure/iot-operations-sdks".insteadOf "https://github.com/Azure/iot-operations-sdks"
    ```

3. Ensure your `GOPRIVATE` environment variable includes
   `github.com/Azure/iot-operations-sdks` (simply set it to this value if it is
   not already present).

4. Take a dependency on the module(s) you want to use via:
    ```bash
    go get github.com/Azure/iot-operations-sdks/go/mqtt@<version>
    go get github.com/Azure/iot-operations-sdks/go/protocol@<version>
    go get github.com/Azure/iot-operations-sdks/go/services@<version>
    ```

## Set up broker

The samples in this repository assume a broker is running on `localhost`. The Go
`mqtt` module is intended for use with the Azure IoT Operations MQTT broker, but
it is compatible with any MQTTv5 broker.

## Running samples

To run one of the samples, navigate to its directory (`go/samples/<sample>`) and
run its server and/or client via:

```bash
go run ./server
go run ./client
```
