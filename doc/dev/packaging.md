# Packaging

The following document contains developer information on packaging the various SDKs and tools in this repository.

## .NET

The Azure IoT Operations NuGet feed is configured to use the https://api.nuget.org/v3/index.json as an upstream feed. 

If you are receiving the following error, you may need to manually refresh the upstream dependencies to the Azure IoT Operations feed.

```output
Response status code does not indicate success: 401 (Unauthorized - No local versions of package '***'; please provide authentication to access versions from upstream that have not yet been saved to your feed.
```

To refresh the dependencies, execute the following:

1. Create a [personal access token](https://dev.azure.com/azure-iot-sdks/_usersSettings/tokens) with with `Packaging | Read & write` permissions.

1. Authenticate using the PAT you created:

    ```bash
    dotnet nuget update source preview -u {USERNAME} -p {PAT_TOKEN} --store-password-in-clear-text
    ```

1. Restore the SDK project to pull dependencies from upstream:

    ```bash
    cd dotnet
    dotnet restore --no-cache
    ```

1. Repeat for the `codegen`:

    ```bash
    cd ../codegen
    dotnet restore --no-cache
    ```

1. Repeat for the `faultablemqttbroker`:

    ```bash
    cd ../eng/test/faultablemqttbroker/src/Azure.Iot.Operations.FaultableMqttBroker
    dotnet restore --no-cache
    ```

## Rust

To pull the required dependencies from upstream crates.io into Azure IoT Operations SDKs feed, execute the following:

1. Create a [personal access token](https://dev.azure.com/azure-iot-sdks/_usersSettings/tokens) with with `Packaging | Read & write` permissions.

1. Authenticate using the PAT:

    ```bash
    export PAT=<PAT_TOKEN>
    cd rust
    echo -n Basic $(echo -n PAT:$PAT | base64) | cargo login --registry aio-sdks
    ```

1. Change into the rust directory and publish the crates:

    ```bash
    cargo publish --manifest-path azure_iot_operations_mqtt/Cargo.toml --registry aio-sdk
    cargo publish --manifest-path azure_iot_operations_protocol/Cargo.toml --registry aio-sdk
    cargo publish --manifest-path azure_iot_operations_services/Cargo.toml --registry aio-sdk
    ```

1. **[Optional]** Build the rumqttc dependency:

    ```bash
    cargo publish --manifest-path rumqttc/Cargo.toml --registry aio-sdk --features use-native-tls --no-default-features
    ```
