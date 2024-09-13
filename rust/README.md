# Azure IoT Operations Rust SDK

This directory contains the source code, examples and tests for the Azure IoT Operations Rust SDK.

## Getting started

The following Azure IoT Operations crates are available:
| Crate | Description |
| - | -|
| [**azure_iot_operations_mqtt**](../azure_iot_operations_mqtt/) | Flexible MQTT client types and traits for async applications |
| [**azure_iot_operations_protocol**](../azure_iot_operations_protocol/) | Types and traits for using the Azure IoT Operations Protocol |
| **azure_iot_operations_services** | COMING SOON

## Installing crates

> [!CAUTION]
> These crates are currently in preview and are subject to change until version 1.0.
> Pinning a specific release will protect you from any breaking changes, which are subject to occur until we release 1.0.

1. Ensure your git credentials are set in your environment, as you will need them to access this repository and take a dependency on the crates within it.

    **You can probably skip this step, since most people already have this set up.**

    ```
    $ git config --global user.name "Your Name Here"
    $ git config --global user.email myemail@example.com
    ```

2. Install the SSL toolkit
    #### Linux
    ```bash
    sudo apt-get install libssl-dev
    ```

    #### Windows
    While this can be done on a Windows development environment, we would at this time advise you to simply use WSL and follow the above Linux instructions.

3. Take a dependency on the crate(s) you need to use in your `Cargo.toml` file for your application.
    ```toml
    [dependencies]
    azure_iot_operations_mqtt = { git = "https://github.com/Azure/iot-operations-sdks.git", tag = "<release tag here>"}
    azure_iot_operations_protocol = { git = "https://github.com/Azure/iot-operations-sdks.git", tag = "<release tag here>" }
    ```
    > We recommend the use of a `tag` parameter to pin a [specific release](https://github.com/Azure/iot-operations-sdks/releases), but you may also use `rev` for a particular commit or pull.
    >To get the latest build, just pass the `git` argument by itself, with no other parameters, although this is not recommended.

#### Deploy MQ broker?
#### Run samples?