# ADR3: Protocol Versioning 0.1

## Status: 

PROPOSED

## Context: 

Our AIO components are not yet in preview, and our protocol is not finalized. However, our protocol is currently at version 1.0, which is counterintuitive.

## Decision: 

All languages will use protocol version 0.1 instead of 1.0 to indicate that our protocol is not finalized. 

This version will not be backward compatible with 1.0; rather, it is a redefinition and update to our protocol version. 

The SDKs will assume the protocol version is 0.1 if the `__protVer` header is not present. Once the protocol goes GA, the assumed protocol version will be `1.0` again and every compliant client will include a protocol version number.

## Protocol Version 0.1:
  - `{senderID}` in the topic for telemetry envoys is no longer required. If included, it will be handled like other generalized topic tokens.
  - Both `TelemetrySender` and `CommandInvoker` will use `__srcId` to specify their client ID.
  - The `TelemetryReceiver` does not require `__srcId`, but the `CommandExecutor` does. If not included:
    - The `TelemetryReceiver` will not provide the sender's ID to the application.
    - The `CommandExecutor` will return an error indicating a missing field. 

