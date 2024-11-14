# ADR3: Protocol Versioning 0.1

## Status: 

PROPOSED

## Context: 

In the context of the recent changes to [generalized topic tokens](./0001-generalized-topic-tokens.md) across all SDKs, there remains a scenario where a telemetry receiver might expect a `{senderId}` token. In the proposed version 1.1 (TODO: Insert link), we will extract `{senderId}` from the topic if the new `__srcId` header is absent. This requirement will mean we'll need to support specific topic tokens, which contradicts the spirit of generalized topic tokens. 

Without `{senderId}`, telemetry envoys can still convey and receive sender information via the `srcId` header, which will replace `invId` for command envoys. This change raises issues for services already using `invId` as the envoy would no longer support it.

## Decision: 

All languages will use protocol version 0.1 instead of 1.0 to indicate that our protocol is not finalized. 

This version will not be backward compatible with 1.0; rather, it is a redefinition and update to our protocol version. 

The SDKs will assume the protocol version is 0.1 if the header is not present.

## Protocol Version 0.1:
  - `{senderID}` in the topic for telemetry envoys is no longer required. If included, it will be handled like other generalized topic tokens.
  - Both `TelemetrySender` and `CommandInvoker` will use `__srcId` to specify their client ID.
  - The `CommandExecutor` and `TelemetryReceiver` do not require `__srcId`. If not included:
    - The `TelemetryReceiver` will not provide the sender's ID to the application.
    - The `CommandInvoker` will return an error indicating a missing field. 

