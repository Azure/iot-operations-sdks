# Rust samples

> [!CAUTION]
>
> The samples and tutorials provided in this repository are for **demonstration purposed only**, and should not be deployed to a production system or included within an application without a full understanding of how they operate.
>
> Additionally some samples may use a username and password for authentication. This is used for sample simplicity and a production system should use a robust authentication mechanism such as certificates.

## Crate samples

Each crate contains a examples directory containing samples demonstrating the usage of its API:

1. [MQTT samples](/rust/azure_iot_operations_mqtt/examples)
1. [Protocol samples](/rust/azure_iot_operations_protocol/examples)
1. [Services samples](/rust/azure_iot_operations_services/examples)

Run the sample, substituting with the sample name:

```bash
cargo run --example <sample name>
```

> [!TIP]
> Do **not** include the `.rs` extension in the sample name.

## SDK samples

This directory contains higher-level samples that show a set of related applications that can be built using the various components of the Rust SDK.

1. [Counter sample](counter)
