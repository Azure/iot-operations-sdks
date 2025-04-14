# Stub Service

The Stub Service provides a local implementation of services that typically require a full deployment of AIO. It is designed for inner loop development.

The Stub Service will also be used for basic happy path testing on the SDKs.

## Features

- **Service Simulation**: Simulates basic service behavior based on DTDL contracts.
  - See [Schema Registry Stub Service Behavior](#schema-registry).
- **Session Isolation**: Each service operates in its own session with a unique MQTT client ID.
- **State and Logs**: Writes state and logs to a folder specified by the environment variable `STUB_SERVICE_OUTPUT_DIR`.
  - This feature is enabled by default with the Rust feature `enable-output`.
  - The folder structure follows this format: `stub_service_[timestamp]`.
  - Each service creates its own subfolder within the main folder to store its state and logs (each under a respective `state` and `logs` folder)
  - See [Example Output Folder](#schema-registry-output-sample).
- **Unified Execution**: All stub services run from the same crate. A critical failure in any service causes a crash, with the error returned from `main`.
- **Logging**: Adheres to [ADR 0005](../../doc/dev/adr/0005-logging.md).

## Stub Services

### Schema Registry

#### State Management

Stores schemas in an internal hashmap with the key being the hash of the content and the value being a list of the versions of the schema.

#### Supported Operations

1. **Get Schema**
   - Retrieves a schema by version.
   - Returns `None` if the schema does not exist.
   - *Errors*: Will be implemented once the DTDL is updated with error definitions.

2. **Put Schema**
   - Stores a schema with a specific version.
   - Returns the stored schema along with its hash, name, and namespace.
   - *Errors*: Will be implemented once the DTDL is updated with error definitions.

#### Schema Registry Output Sample

```text
folder [STUB_SERVICE_OUTPUT_DIR]
  folder stub_service_1743702989
  ├── folder SchemaRegistry
  | ├── folder logs
  | │   ├── logs.json
  | ├── folder state
  | │   ├── foo_schema.json
  | │   ├── bar_schema.json
  folder stub_service_1743700000
  ├── folder SchemaRegistry
  | ├── folder logs
  | │   ├── logs.json
  | ├── folder state
  | │   ├── foo_schema.json
```
