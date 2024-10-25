# Reference Documentation

The SDKs in this repository are built on open standards wherever possible, such as MQTT.

This directory contains documentation relating to the use of the SDKs, as well as the underlying topic and payload structure used for communication.

## Reference Topics

| State |Topic | Description |
|-|-|-|
| rewrite | [Commands](command.md) | Describes at a high level how the RPC protocol is adapted to Command Execution |
| rewrite | [Command Cache](command-cache.md) | The command cache is used for de-duplicating requests to avoid multiple invocation during disconnection |
| update | [Command Errors](command-errors.md) | Outline the different error conditions that arise during Command execution and how these are communicated to the user. |
| update | [Command Timeouts](command-timeouts.md) | Command timeouts are used during command execution. This document describes how the different timeouts are resolved to a predictable behavior |
| rewrite | [Connection Management](connection-management.md) | Outlines the strategies that are undertaken to predictable response to different type of connection loss |
| update | [Connection Settings](connection-settings.md) | Outlines the parameters of MQTT settings long with the associated environment variables and default value |
| update | [Error Model](error-model.md) | Describes the different types of errors reported by the SDKs during exceptional circumstances |
| update | [Message Metadata](message-metadata.md) | Describes the user and system properties used across Telemetry and Commands |
| remove | [Payload Format](payload-format.md) | Serialization format definitions that are planned to be implemented by the SDKs |
| rewrite | [RPC Protocol](rpc-protocol.md) | Details on the RPC implementation, used by the Commands |
| update | [Session Client](session-client.md) | Details on the session client implementation |
| update | [Shared Subscriptions](shared-subscriptions.md) | How shared subscriptions are implemented with the Command model and what the expected behavior is |
| update | [Telemetry](telemetry.md) | Outline of the responsibilities of the Telemetry sender and receiver |
| update | [Topic Structure](topic-structure.md) | The format of the MQTT topic used to communicate between applications using the Telemetry and Command API's |

## Developer notes

| Topic | Description |
|-|-|
| [Package Versioning](package-versioning.md) | Outline of the package versioning strategy implemented by the SDKs |
| [Protocol Versioning](protocol-versioning.md) | Describes how changing protocol versions are managed across different package versions |
| [Repository Structure](repository-structure.md) | The directory structure used by this repository |
| [RPC Protocol Testing](rpc-protocol-testing.md) | Strategies to effective test the RPC protocol implementation |

| prune[Session Client Testing](session-client-testing.md) | Unit test definitions for testing the connection management |
| [Telemetry Protocol Testing](telemetry-protocol-testing.md) | Unit test definitions for testing the Telemetry protocol |
