# Azure IoT Operations Edge Registry CLI

## Description

`xregistry-cli` manages Schemas, Thing Models, and Thing Descriptions in Azure
IoT Operations Edge Registry. It supports adding, retrieving, listing, and
deleting document versions, and can seed built-in demonstration resources.

The tool currently connects without TLS to an MQ broker at `localhost:1883`.
Each registry request has a default timeout of 10 seconds. Use
`--timeout-seconds` to change it. The value must be a whole number from 1 to
4294967295, as required by the SDK.

## Build

```powershell
Set-Location ./tools/xregistry-cli
cargo build --release --locked
```

The executable is produced at:

```text
tools/xregistry-cli/target/release/xregistry-cli
```

## Usage

Display the complete command reference:

```powershell
./target/release/xregistry-cli --help
```

Set a 30-second timeout for each registry request:

```powershell
./target/release/xregistry-cli --timeout-seconds 30 list thing-model
```

The timeout option can appear before or after the command.

Add a Thing Model:

```powershell
./target/release/xregistry-cli add thing-model counter ./Counter.TM.json `
    --label environment=test
```

`--label` applies the same label to both the parent Resource and the new Version.
This option can be repeated.

Add a Thing Description:

```powershell
./target/release/xregistry-cli add thing-description counter-01 ./Counter.TD.json
```

Add a JSON Schema Draft-07 document:

```powershell
./target/release/xregistry-cli add schema counter ./Counter.schema.json
```

Retrieve the default Thing Model Version:

```powershell
./target/release/xregistry-cli get thing-model counter
```

List Thing Model Versions:

```powershell
./target/release/xregistry-cli list thing-model counter
```

Delete Thing Model Version 1:

```powershell
./target/release/xregistry-cli delete thing-model counter --version 1
```

Seed the built-in demonstration Schema, Thing Model, and Thing Description:

```powershell
./target/release/xregistry-cli seed-demo
```

The Thing Description contains an example endpoint. The seed command does not
start a device service. If the older demo Thing Description is present, the
command adds a corrected Version with property forms. It does not replace
existing Versions.
