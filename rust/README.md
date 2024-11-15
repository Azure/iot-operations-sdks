# Azure IoT Operations Rust SDK

This directory contains the source code, examples and tests for the Azure IoT Operations Rust SDK.

## Overview

The following Azure IoT Operations crates are available:

| Crate | Description |
| - | -|
| [**azure_iot_operations_mqtt**](azure_iot_operations_mqtt) | MQTTv5 client library for decoupled asynchronous applications |
| [**azure_iot_operations_protocol**](azure_iot_operations_protocol) | Utilities for using the Azure IoT Operations Protocol (RPC, Telemetry) |
| [**azure_iot_operations_services**](azure_iot_operations_services) | Clients for using services of Azure IoT Operations |

## Getting started

### Rust
To set up and get familiar with the Rust language, there are several resources available in our [Rust Resources](/doc/dev/rust_resources.md) guide.

### Azure IoT Operations
To set up your cluster and install Azure IoT Operations, refer to the [setup guide](/doc/setup.md)

## Installing crates

> [!CAUTION]
> These crates are currently in preview and are subject to change until version 1.0.
> Pinning a specific release will protect you from any breaking changes, which are subject to occur until we release 1.0.

1. Install the SSL toolkit:

    ### Ubuntu/Debian Linux

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

The Azure IoT Operations MQTT crate is intended for use with the Azure IoT Operations MQ broker, but is compatible with any MQTTv5 broker, local or remote.

## Running samples

### Crate samples
Each crate has its own set of samples demonstrating the usage of its API. They can be found in the `examples` directory inside the particular crate.

To run a sample for one of the crates run the command:

```bash
cargo run --example <sample name>
```

> [!NOTE]
> You should **not** include the `.rs` extension in the sample name.

These samples may assume the use of a broker running on `localhost`, or settings/credentials supplied by [environment variables](/doc/reference/connection-settings.md). They can be modified to supply different settings/credentials as necessary.

### SDK samples
Additionally there are higher-level samples that show a set of related applications that can be built using the various components of the Rust SDK, including codegen. They can be found in the [`sample_applications`](sample_applications) directory, along with instructions for running them.
