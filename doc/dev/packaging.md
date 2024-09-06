# Packaging

## C#

The IoT Operations NuGet feed is configured to use the https://api.nuget.org/v3/index.json as the upstream feed. 

If you are receiving the following error, you may need to manually refresh the upstream dependencies in IoT Operations feed.

```output
Response status code does not indicate success: 401 (Unauthorized - No local versions of package '***'; please provide authentication to access versions from upstream that have not yet been saved to your feed.
```

To refresh the dependencies, execute the following:

1. Create a [personal access token](https://dev.azure.com/azure-iot-sdks/_usersSettings/tokens) with with `Packaging | Read & write` permissions:

1. Change into the `dotnet` directory, authenticate using the PAT from previous step and restore the project:

    ```bash
    cd dotnet
    dotnet nuget update source preview -u {USERNAME} -p {PAT} -n preview --store-password-in-clear-text
    dotnet restore --no-cache
    ```

1. Repeat for the `codegen` directory:

    ```bash
    cd ../codegen
    dotnet restore --no-cache
    ```
