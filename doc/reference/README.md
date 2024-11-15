# Reference Documentation

The SDKs within this repository are built on open standards wherever possible, such as MQTT v5.

This directory contains documentation relating to the implementation of the SDKs, as well as the underlying topic and payload structure used for communication over MQTT.

> [!CAUTION]
> Due to the preview nature of the SDKs, many of the documents below are not 100% up to date with implementation. The `State` column reflects the current status as of **November 2024**.

| State |
|-|
| :green_circle: Recently updated |
| :yellow_circle: Update needed |
| :red_circle: Rewrite needed |

## Reference topics

| State |Topic | Description |
|-|-|-|
| :red_circle: | [Commands](commands.md) | Describes at a high level how the RPC protocol is adapted to Command Execution |
| :red_circle: | [Command Cache](command-cache.md) | The command cache is used for de-duplicating requests to avoid multiple invocation during disconnection |
| :yellow_circle: | [Command Errors](command-errors.md) | Outline the different error conditions that arise during Command execution and how these are communicated to the user. |
| :yellow_circle: | [Command Timeouts](command-timeouts.md) | Command timeouts are used during command execution. This document describes how the different timeouts are resolved to a predictable behavior |
| :red_circle: | [Connection Management](connection-management.md) | Outlines the strategies that are undertaken to predictable response to different type of connection loss |
| :yellow_circle: | [Connection Settings](connection-settings.md) | Outlines the parameters of MQTT settings long with the associated environment variables and default value |
| :yellow_circle: | [Error Model](error-model.md) | Describes the different types of errors reported by the SDKs during exceptional circumstances |
| :yellow_circle: | [Message Metadata](message-metadata.md) | Describes the user and system properties used across Telemetry and Commands |
| :red_circle: | [RPC Protocol](rpc-protocol.md) | Details on the RPC implementation, used by the Commands |
| :yellow_circle: | [Session Client](session-client.md) | Details on the session client implementation |
| :green_circle: | [Shared Subscriptions](shared-subscriptions.md) | How shared subscriptions are implemented with the Command model and what the expected behavior is |
| :yellow_circle: | [Telemetry](telemetry.md) | Outline of the responsibilities of the Telemetry sender and receiver |
| :yellow_circle: | [Topic Structure](topic-structure.md) | The format of the MQTT topic used to communicate between applications using the Telemetry and Command API's |

## Additional topics

| State | Topic | Description |
|-|-|-|
| :green_circle: | [Package Versioning](package-versioning.md) | Outline of the package versioning strategy implemented by the SDKs |
| :green_circle: | [Protocol Versioning](protocol-versioning.md) | Describes how changing protocol versions are managed across different package versions |
| :green_circle: | [Repository Structure](repository-structure.md) | The directory structure used by this repository |
