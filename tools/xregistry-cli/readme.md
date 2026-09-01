# Azure IoT Operations Edge Registry CLI

## Description

`xregistry-cli` manages Schemas, Thing Models, and Thing Descriptions in Azure
IoT Operations Edge Registry. It supports adding, retrieving, listing, and
deleting document versions, and can seed built-in demonstration resources.

The tool currently connects without TLS to an MQ broker at `localhost:1883`.

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

Add a Thing Model:

```powershell
./target/release/xregistry-cli add thing-model counter ./Counter.TM.json `
    --label environment=test
```

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

Seed the built-in demonstration Thing Model and Thing Description:

```powershell
./target/release/xregistry-cli seed-demo
```
