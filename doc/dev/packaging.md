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

1. Bump the selected crate's version as appropriate and merge the changes into main.

1. Create the tag for the commit hash according to the pattern: `rust/{mqtt|protocol|services|connector}/v{version}`. The version must match the selected crate's `Cargo.toml`, including any prerelease suffix.

1. Queue the pipeline configured from [the Rust release YAML](../../.pipelines/release-rust-azure-iot-operations-sdks.yml) using the parameters:
    - Default `main` pipeline branch for the pipeline version.
    - The tag you created in the previous step for the `tag` parameter.
    - Set `dry_run` to `true` to package without publishing, or `false` to release to crates.io. The default remains `false`, matching the original pipeline.

1. During the run of the pipeline you will require someone other than yourself with a SAW to approve the release using the link provided from the `ApprovalService` in the final stage of the pipeline.

### Rust release pipeline setup

Create the official pipeline through StartRight in `msazure/One`, using this GitHub
repository and [the Rust release YAML](../../.pipelines/release-rust-azure-iot-operations-sdks.yml).
The YAML uses standard 1ES Pipeline Templates rather than OneBranch. It retains
the original `tag` and `dry_run` parameters and validates the tag format and the
`mqtt`, `protocol`, `services`, or `connector` crate selection. It also checks that
the tag version matches the packaged crate's manifest version.

Authorize the pipeline to use:

- The `1ESPipelineTemplates/1ESPipelineTemplates` template repository.
- The existing `akri-mqtt-patterns-github-connection` GitHub service connection, so
  the SDK source can be checked out at the requested tag independently of the
  branch containing the pipeline YAML.
- The `iot-operations-1es-pool2` pool and its `iot-operations-1es-image` image.
  This is a Windows Server 2022 image, used for packaging, source analysis, and
  release. Pool and image names are compile-time template inputs and remain
  literal values in the YAML.
- The existing ESRP service connection and the `aio-sdk-esrp` variable group.

Create the variable group in the Azure DevOps Library before creating the pipeline
definition. Use `aio-sdk-esrp` for shared, language-independent ESRP configuration.
Other language release pipelines can reuse this group when the same ESRP identity
and onboarding authorize their content type and publishing destination. Populate
its variables from the corresponding task inputs in the
[existing AIO release YAML](https://msazure.visualstudio.com/One/_git/iot-operations-sdks-pipelines?path=%2Frelease-rust-azure-iot-operations-sdks.yml);
do not copy the private configuration values into this repository.

#### Shared ESRP variables (`aio-sdk-esrp`)

| Variable | Value to configure in Azure DevOps |
| --- | --- |
| `ESRP_SERVICE_CONNECTION` | Existing AIO `connectedservicename`. |
| `ESRP_KEYVAULT_NAME` | Existing AIO `keyvaultname`. |
| `ESRP_SIGN_CERT_NAME` | Existing AIO `signcertname`. |
| `ESRP_CLIENT_ID` | Existing AIO `clientid`. |
| `ESRP_OWNERS` | Existing AIO `owners` list. |
| `ESRP_APPROVERS` | Existing AIO `approvers` list. |
| `ESRP_DOMAIN_TENANT_ID` | Existing AIO `domaintenantid`. |

Keep resource names available as nonsecret variables for pipeline
resource resolution; mark confidential values as secrets. Reuse the existing AIO
ESRP managed identity, Key Vault, signing certificate, owners, approvers, and tenant
configuration rather than creating a new ESRP onboarding.

#### Rust toolchain and dependency feeds

The credential-free feed URLs are inlined in the `RustInstaller@1` task; no
Rust-specific variable group is required. The task installs Rust from the
`RustTools` NuGet feed and uses the existing `azure_iot_operations` Cargo mirror,
both in `msazure/One`. Authentication uses `System.AccessToken`, not credentials
embedded in the URLs.

Before the first run, create the `RustTools` feed if it is not already available
and configure the Microsoft Rust toolchain upstream following the
[Rust feed setup guidance](https://eng.ms/docs/more/languages-at-microsoft/rust/articles/gettingstarted/install/ci/azure-artifacts-feeds).
Grant the pipeline's build identity Feed and Upstream Reader (Collaborator)
permission on both feeds so it can save new upstream packages. See the
[RustInstaller task reference](https://eng.ms/docs/more/languages-at-microsoft/rust/articles/gettingstarted/install/rustinstallerreference).

The build produces only the selected crate, retaining the original
`cargo package --no-verify` behavior; SDK compilation and tests remain in Rust CI.
Artifacts are published and downloaded using the
[standard 1ES build-output and release-input declarations](https://eng.ms/docs/coreai/devdiv/one-engineering-system-1es/1es-docs/1es-pipeline-templates/features/releasepipelines/releaseworkflows/releasejob),
without custom REST downloads or shell-based Rust installation. Dry runs skip the
entire production release stage.

Complete a `dry_run: true` run before replacing the
[legacy OneBranch release pipeline](https://dev.azure.com/msazure/One/_build?definitionId=442088).
