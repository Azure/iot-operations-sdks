# Azure IoT Operations SDKs

Multi-language SDKs (Rust, .NET, Go) for building edge applications that run on Azure Arc-enabled Kubernetes clusters and integrate with Azure IoT Operations services.

## Build, Test, and Lint Commands

### .NET

```bash
# Build
dotnet build dotnet/Azure.Iot.Operations.sln

# Run all tests (requires MQTT broker running - see Integration Tests below)
dotnet test dotnet/Azure.Iot.Operations.sln

# Run a single test class
dotnet test dotnet/Azure.Iot.Operations.sln --filter "FullyQualifiedName~StateStoreClientTests"

# Run a specific test
dotnet test dotnet/Azure.Iot.Operations.sln --filter "FullyQualifiedName~TestGetAndSetKey"

# Lint (enforced via Directory.Build.props with TreatWarningsAsErrors=true)
dotnet build dotnet/Azure.Iot.Operations.sln  # Warnings are errors
```

### Go

Uses [mage](https://magefile.org/) build tool via `magefile.go` in each module.

```bash
cd go/<module>  # e.g., go/protocol, go/mqtt, go/services

# Run all CI checks (format, lint, doc, test)
mage ci

# Format code (line length max 80, uses golines)
mage format

# Lint (uses golangci-lint v1.64.5)
mage lint

# Run tests (with race detection, 10s timeout)
mage test

# Run tests without cache
mage testclean

# Generate API docs
mage doc
```

### Rust

```bash
cd rust

# Run checks (formatting, copyright headers, clippy, unused deps)
make check

# Run tests
make test

# Run tests with specific features
FEATURES="all" make test

# Run tests with network tests enabled
ENABLE_NETWORK_TESTS=true make test

# Run a single test
cargo test --package azure_iot_operations_mqtt test_name

# Run clippy directly
cargo clippy --all --tests --examples -- --deny=warnings

# Format check
cargo fmt --verbose --all --check

# Coverage report
make coverage
```

### Protocol Compiler (Codegen)

```bash
# Build codegen
dotnet build codegen/codegen.sln

# Install globally
dotnet tool install -g Azure.IoT.Operations.ProtocolCompiler --add-source https://pkgs.dev.azure.com/azure-iot-sdks/iot-operations/_packaging/preview/nuget/v3/index.json

# Generate from DTDL model
Azure.Iot.Operations.ProtocolCompiler --modelFile <path> --lang <csharp|go|rust>
```

## Integration Tests

Integration tests require an MQTT broker. Use the Codespaces devcontainer or run `tools/deployment/configure-aio.sh` after deploying Azure IoT Operations to your cluster.

For .NET integration tests, set environment variable:
```bash
MQTT_TEST_BROKER_CS="HostName=127.0.0.1;TcpPort=8884;UseTls=true;ClientId=TestClient;CaFile=<ca-path>;SatAuthFile=<token-path>"
```

## Architecture

### SDK Structure (per language)

Each language SDK contains 3-4 parallel packages/modules with consistent functionality:

| Package | Purpose |
|---------|---------|
| **Mqtt** | Session client with connection management, authentication (SAT/x509), and automatic reconnection |
| **Protocol** | Telemetry (sender/receiver) and Command (invoker/executor) patterns built on MQTT5 |
| **Services** | Clients for State Store, Lease Lock, Schema Registry, Azure Device Registry |
| **Connector** | Framework for building Akri connectors (Rust/.NET only) |

### Package Naming

- **.NET**: `Azure.Iot.Operations.{Mqtt|Protocol|Services|Connector}` in `dotnet/src/`
- **Rust**: `azure_iot_operations_{mqtt|protocol|services|connector}` crates in `rust/`
- **Go**: `github.com/Azure/iot-operations-sdks/go/{mqtt|protocol|services}` modules in `go/`

### Key Patterns

**Envoys**: MQTT communication patterns (RPC and telemetry). Terminology:
- Telemetry: sender → receiver
- Commands: invoker → executor (RPC pattern)

**Codegen**: Protocol compiler generates client/server stubs from DTDL (v1 in `/codegen`) or WoT Thing Models (v2 in `/codegen2`).

### Directory Layout

```
/dotnet/          - .NET SDK (Azure.Iot.Operations.sln)
/go/              - Go SDK (go.work workspace)  
/rust/            - Rust SDK (Cargo workspace)
/codegen/         - Protocol compiler (DTDL-based)
/codegen2/        - Protocol compiler v2 (WoT Thing Model-based)
/doc/             - Documentation (user-facing)
/doc/dev/         - Developer/contributor documentation
/eng/test/        - Cross-language testing infrastructure
/samples/         - End-to-end sample applications
/tools/           - Developer tools (state store CLI, deployment scripts)
```

## Key Conventions

### Rust

- All `.rs` files must have Microsoft copyright header (checked in CI)
- All `Cargo.toml` files must include `[lints] workspace = true`
- Workspace lints in root `Cargo.toml`: `rust_2018_idioms = deny`, clippy pedantic enabled
- Unit tests are inline in `src/`; integration tests go in `tests/`

### Go

- Max line length: 80 characters (enforced by golines)
- Each module has its own `magefile.go` importing from `go/internal/mage`
- Go 1.24+ required (see `go.work`)

### .NET

- .NET 9.0 required
- `TreatWarningsAsErrors` enabled globally via `Directory.Build.props`
- Code style enforced via `EnforceCodeStyleInBuild=true`

### Cross-Language

- Tests in `/eng/test/test-cases/Protocol/` run against all language implementations (METL tests)
- Environment variables for broker connection use `AIO_` prefix (e.g., `AIO_BROKER_HOSTNAME`, `AIO_TLS_CA_FILE`, `AIO_SAT_FILE`)

## Package Feeds

- **.NET NuGet**: `https://pkgs.dev.azure.com/azure-iot-sdks/iot-operations/_packaging/preview/nuget/v3/index.json`
- **Rust Cargo**: `https://pkgs.dev.azure.com/azure-iot-sdks/iot-operations/_packaging/preview/Cargo/index/`
- **Go**: Published via GitHub releases, use `go get github.com/Azure/iot-operations-sdks/go/{module}@<version>`

## Deployment

Edge applications deploy to Kubernetes using kubectl. See `/doc/edge_application/deploy.md` for container build and deployment instructions.