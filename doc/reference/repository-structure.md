# Azure IoT Operations SDKs repository structure

This document defines the name and file structure of the GitHub repository for the Azure IoT Operations SDKs.

## Top-level directory layout

The repository will follow [Azure SDKs Repository structure guidelines](https://azure.github.io/azure-sdk/policies_repostructure.html) where possible. The main difference is due to the mono-repository format, it will contain multiple languages which will be separated at the top-level.

| Directory | Contents |
|-|-|
| `/doc/` | Markdown documentation. This will contain both Microsoft Learn staging docs while we are private preview, as well as language agnostic documentation. Language related docs will live in the language directory. |
| `/doc/dev` | Markdown documentation targeted to developers contributing to the repository. |
| `/eng/` | Contains things needed to build test, or perform related tasks. |
| `/eng/test/` | Common testing infrastructure such as the METL tests. Language specific testing will be within each language folder. A README.md will describe the tests and provide links to language specific testing directories |
| `/tools/` | Tools useful for developing with the SDKs, such as the state store CLI. |
|-|-|
| `/rust/` | All files relating to Rust SDK. See [Cargo package layout](https://doc.rust-lang.org/cargo/guide/project-layout.html). | 
| `/go/` | All files relating to the Go SDK. See [Go modules layout](https://go.dev/doc/modules/layout). |
| `/dotnet/` | All files relating to the .NET SDK. See [.NET project structure](https://learn.microsoft.com/dotnet/core/porting/project-structure). |
|-|-|
| `/codegen` | All files relating to the protocol compiler / codegen tool. |

Layout within the languages directory should follow the Azure SDK language specific recommendations where it makes sense combined with the recommended layout for the language itself. It will contains source, tests, samples as well a README.md containing instructions to use the SDK.

## Top-level files

The top-level should only contain files expected for an Microsoft Open-source project, but exceptions may be made where necessary. Associated languages files should be created within that language directory.

## SDK layout

Principles:
- Each language contains the same core packages: mqtt, protocol, services, connector (except for Go).
- Include `src/`, `tests/` (or `test/`), and `samples/`. Other folders are optional (e.g., `examples/`, `templates/`).
- Follow idiomatic language conventions.

Naming conventions by language (illustrative):
- .NET: `Azure.Iot.Operations.{Mqtt|Protocol|Services|Connector}` under `src/`
- Rust: `azure_iot_operations_{mqtt|protocol|services|connector}` (crates)
- Go: `{mqtt|protocol|services}` packages under `go/`

### Language notes

- .NET
  - The Azure.Iot.Operations.sln file will not include a reference to any of the DSS CLI, faultable MQTT broker, schema registry service, or code gen packages.
  - The faultable MQTT broker schema registry service packages will live in `eng/test` since they are both tools to be used only for testing our different language SDKs.
  - The DSS CLI package will live in `tools`.

- Rust
  - Unit tests for each crate are part of `src`. The `tests` subdirectories are for integration/stress/longhaul etc. tests, including METL if possible.
  - Any CodeGen related samples are in the `sample_applications` directory as individual crates.

- Go
  - The current plan of record is for `services` to be a single module, but each of its packages should be structured with minimal interdependency such that they could be converted to separate modules in the future. This would not change the directory structure.
  - `samples` follows the precedent of `Azure/azure-sdk-for-go` and should contain the client/envoy/server triplet for each named sample.
  - `test` contains the common testing infrastructure (e.g. the METL test framework for Go). It should be structured following the `/eng/test/` folder.

## README.md format

Each language directory will contain README.md and will provide information on how to install, use, and test the SDK. Refer to the [Azure SDK guidelines](https://azure.github.io/azure-sdk/general_documentation.html) and [example](https://github.com/Azure/azure-sdk/blob/main/docs/policies/README-EXAMPLE.md) for more information.
 
