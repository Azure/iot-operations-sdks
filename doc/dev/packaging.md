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
    dotnet nuget update source AzureIoTOperations -u $USERNAME -p $PAT_TOKEN --store-password-in-clear-text
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

## Rust SDK

To issue a release for the Rust SDK:

1. Bump the version as appropriate and merge the changes into main

1. Create the tag for the commit hash according to the pattern: `rust/{mqtt|protocol|services|connector}/v{major}.{minor}.{patch}`

1. Use the [Rust SDK release pipeline](https://dev.azure.com/msazure/One/_build?definitionId=442088) using the parameters:
    - default `main` pipeline branch for the pipeline version
    - the tag you created in the previous step for the "tag to release from" parameter
    - do not check "Dry run" (unless you want to test it without releasing)

1. During the run of the pipeline you will require someone other than yourself with a SAW to approve the release using the link provided from the `ApprovalService` in the final stage of the pipeline.
