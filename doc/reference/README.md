# Reference Documentation

The SDKs in this repository are built on open standards wherever possible, such as MQTT.

This directory contains documentation relating to the use of the SDKs, as well as the underlying topic and payload structure used for communication.

## MQTT Reference

| Topic | Description |
|-|-|
| [Command API](command-api.md) | Describes at a high level how the RPC protocol is adapted to an Command Execution |
| [Command Cache](command-cache.md) | The command cache is used for de-duplicating requests to avoid multiple invocation during disconnection |
| [Command Timeouts](command-timeouts.md) | Command timeouts are used during command execution. This document describes how the different timeouts are resolved to a predictable behavior |
| [Connection Management](connection-management.md) | Outlines the strategies that are undertaken to predictable response to different type of connection loss |
| [Envoy Binder](envoy-binder.md) | Defines the contract between a publisher and consumer, such as the telemetry sender/receiver or the RPC invoker/executor. |
| [Error Model](error-model.md) | Describes the different types of errors reported by the SDKs during exceptional circumstances |
| [RPC Protocol](rpc-protocol.md) | Details on the RPC implementation, used by the Command API |
| [Shared Subscriptions](shared-subscriptions.md) | How shared subscriptions are implemented with the Command model and what the expected behavior is |
| [Telemetry API](telemetry-api.md) | Outline of the responsibilities of the Telemetry senderand receiver |
| [Topic Structure](topic-structure.md) | The format of the MQTT topic used to communicate between applications using the Telemetry and Command API's |

## SDK Reference

| Topic | Description |
|-|-|
| [Connection Settings](connection-settings.md) | Outlines the parameters of MQTT settings long with the associated environment variables and default value |
| [Command Errors](command-errors.md) | Outline the different error conditions that arise during Command execution and how these are communicated to the user. |
| [Message Metadata](message-metadata.md) | Describes the user and system properties used across Telemetry and Commands |
| [Payload Format](payload-format.md) | Serialization format definitions that are planned to be implemented by the SDKs |

## Developer notes

| Topic | Description |
|-|-|
| [Package Versioning](package-versioning.md) | Outline of the package versioning strategy implemented by the SDKs |
| [Protocol Versioning](protocol-versioning.md) | Describes how changing protocol versions are managed across different package versions |
| [Repository Structure](repository-structure.md) | The directory structure used by this repository |
| [RPC Protocol Testing](rpc-protocol-testing.md) | Strategies to effective test the RPC protocol implementation |
| [Session Client Testing](session-client-testing.md) | Unit test definitions for testing the connection management |
| [Telemetry Protocol Testing](telemetry-protocol-testing.md) | Unit test definitions for testing the Telemetry protocol |
