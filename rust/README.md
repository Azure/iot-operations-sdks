# Azure IoT Operations Rust SDK

This directory contains the source code, examples and tests for the Azure IoT Operations Rust SDK.

## Overview

The following Azure IoT Operations crates are available:

| Crate | Description |
| - | -|
| [**azure_iot_operations_mqtt**](azure_iot_operations_mqtt) | MQTTv5 client library for decoupled asynchronous applications |
| [**azure_iot_operations_protocol**](azure_iot_operations_protocol) | Utilities for using the Azure IoT Operations Protocol (RPC, Telemetry) |
| [**azure_iot_operations_services**](azure_iot_operations_services) | Clients for using services of Azure IoT Operations |

## Getting started with Rust

To get familiar with the Rust language, there are several resources available in our [Rust Resources](/doc/dev/rust_resources.md) guide.

## Installing crates

> [!CAUTION]
> These crates are currently in preview and are subject to change until version 1.0.
> Pinning a specific release will protect you from any breaking changes, which are subject to occur until we release 1.0.

1. Install the SSL toolkit:

    ### Linux

    ```bash
    sudo apt-get install libssl-dev pkg-config
    ```

    ### Windows

    While this can be done on a Windows development environment, we would at this time advise you to simply use WSL and follow the above Linux instructions.


### Using crate registry (recommended)
2. Add the Azure IoT Operations SDK crate feed by adding the following to `config.toml` as described [in the Cargo book](https://doc.rust-lang.org/cargo/reference/config.html):

    ```toml
    [registries]
    aio-sdks = { index = "sparse+https://pkgs.dev.azure.com/azure-iot-sdks/iot-operations/_packaging/preview/Cargo/index/" }
    ```

3. Take a dependency on the crate(s) you want to use in your `Cargo.toml` file for your application, specifying the `aio-sdks` registry configured above:

    ```toml
    [dependencies]
    azure_iot_operations_mqtt = { version = "<version>", registry = "aio-sdks" }
    azure_iot_operations_protocol = { version = "<version>", registry = "aio-sdks" }
    azure_iot_operations_services = { version = "<version>", registry = "aio-sdks" }
    ```

### Using nightly builds (not recommended for most users)
2. Take a dependency on the crate(s) you want to use in your `Cargo.toml` file for your application, specifying the commit SHA of the nightly build you want:
    ```toml
    [dependencies]
    azure_iot_operations_mqtt = { git = "https://github.com/Azure/iot-operations-sdks.git", rev = "<commit SHA here>"}
    azure_iot_operations_protocol = { git = "https://github.com/Azure/iot-operations-sdks.git", rev = "<commit SHA here>" }
    azure_iot_operations_services = { git = "https://github.com/Azure/iot-operations-sdks.git", rev = "<commit SHA here>" }
    ```


    * Note that using a nightly build requires a GH credential, which is difficult to work with in automated deployments.
    * Note also that directly referencing different release tags can create dependency issues, thus the recommendation of using a SHA.

## Set up broker

The samples in this repository assume a broker is running on `localhost`.
The Azure IoT Operations MQTT crate is intended for use with the Azure IoT Operations MQ broker, but are compatible with any MQTTv5 broker, local or remote.

## Running samples

To run a sample for one of the crates, navigate to its respective directory and run the command

```bash
cargo run --example <sample name>
```

> [!NOTE]
> You should **not** include the `.rs` extension in the sample name.
