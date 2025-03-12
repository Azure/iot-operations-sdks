# Azure IoT Operations Rust SDK

This directory contains the source code, examples and tests for the Azure IoT Operations Rust SDK.

> [!CAUTION]
> These crates are currently in preview and are subject to change until version 1.0.
> Pinning a specific release will protect you from any breaking changes, which are subject to occur until we release 1.0.

## Overview

The following Azure IoT Operations crates are available:

| Crate | API | Description |
|-|-|-|
| [**azure_iot_operations_mqtt**](azure_iot_operations_mqtt) | [:link:](https://azure.github.io/iot-operations-sdks/rust/azure_iot_operations_mqtt/) | MQTTv5 client library for decoupled asynchronous applications |
| [**azure_iot_operations_protocol**](azure_iot_operations_protocol) | [:link:](https://azure.github.io/iot-operations-sdks/rust/azure_iot_operations_protocol/) | Utilities for using the Azure IoT Operations Protocol (RPC, Telemetry) |
| [**azure_iot_operations_services**](azure_iot_operations_services) | [:link:](https://azure.github.io/iot-operations-sdks/rust/azure_iot_operations_services/) | Clients for using services of Azure IoT Operations |

## Getting started

1. To set up your cluster and install Azure IoT Operations, refer to the [setup guide](/doc/setup.md)

1. Check out the available [Rust samples](#samples)

1. Read through [Deploy the application](/doc/edge_application/deploy.md) for building and deploying the container to your K8s cluster

## Installing the crates

We recommend using Ubuntu or Debian for developing your applications. The instructions below may require modifications for other Linux distributions.

1. Install the SSL toolkit:

    ```bash
    sudo apt-get install libssl-dev pkg-config
    ```

2. Add our crate feed by adding the following to `config.toml` as described [in the Cargo book](https://doc.rust-lang.org/cargo/reference/config.html):

    ```toml
    [registries]
    aio-sdks = { index = "sparse+https://pkgs.dev.azure.com/azure-iot-sdks/iot-operations/_packaging/preview/Cargo/index/" }
    ```

3. Add the crates you wish to use to your application's `Cargo.toml`:

    ```toml
    [dependencies]
    azure_iot_operations_mqtt = { version = "<version>", registry = "aio-sdks" }
    azure_iot_operations_protocol = { version = "<version>", registry = "aio-sdks" }
    azure_iot_operations_services = { version = "<version>", registry = "aio-sdks" }
    ```

### Unreleased builds

> [!CAUTION]
> Using unreleased builds is not recommended unless directed by the development team, as functionality may not work correctly and no support will be provided.

Take a dependency on the crates you wish to use in your applications `Cargo.toml`, specifying the commit SHA of the nightly build you want:

   ```toml
   [dependencies]
   azure_iot_operations_mqtt = { git = "https://github.com/Azure/iot-operations-sdks.git", rev = "<commit SHA here>"}
   azure_iot_operations_protocol = { git = "https://github.com/Azure/iot-operations-sdks.git", rev = "<commit SHA here>" }
   azure_iot_operations_services = { git = "https://github.com/Azure/iot-operations-sdks.git", rev = "<commit SHA here>" }
   ```

> [!NOTE]
> * Due to the repository being private, using a nightly build requires a GH credential, which is difficult to work with in automated deployments.
> * Referencing different release tags can create dependency issues, it's recommended to use a common SHA across the packages.

## Samples

> [!CAUTION]
>
> The samples and tutorials provided in this repository are for **demonstration purposed only**, and should not be deployed to a production system or included within an application without a full understanding of how they operate.
>
> Additionally some samples may use a username and password for authentication. This is used for sample simplicity and a production system should use a robust authentication mechanism such as certificates.

### Crate samples

Each crate contains an examples directory containing samples demonstrating the usage of its API:

1. [MQTT samples](/rust/azure_iot_operations_mqtt/examples)
1. [Protocol samples](/rust/azure_iot_operations_protocol/examples)
1. [Services samples](/rust/azure_iot_operations_services/examples)

Run the sample, substituting the sample name of your choice:

```bash
cargo run --example <sample name>
```

> [!TIP]
> Do **not** include the `.rs` extension in the sample name.

### SDK samples

There are also higher-level samples that show a set of related applications that can be built using the various components of the Rust SDK. They can be found in the [sample_applications](./sample_applications) directory.
