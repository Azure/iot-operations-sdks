# ADR 18: Mandatory Client ID in Connection Settings

## Context

MQTTv5 specification requires a Client ID to be present in the CONNECT packet payload (3.1.3-3), however it also allows the length of the Client ID to be 0 bytes IF the broker supports assigning it's own unique identifier to the client (3.1.3-6).

Currently, [our specification for connection settings](../../reference/connection-settings.md) allows Client ID to be optional, relying upon this optional broker behavior.

However, allowing the broker to assign the Client ID has logical implications for Session Clients - it means that the client ID is not known until after a connect (problematic for application configuration of envoys in some implementations). Furthermore, it prevents us from being able to leverage `clean_start = false` to resume an existing MQTT Session, as we would be assigned a new Client ID every connection.

## Decision

Since we are building an SDK based around Session Clients that maintain MQTT Session, we should not entertain the broker-assigned client ID scenario, and require customers to provide a client ID in the Connection Settings or Session Client constructor as appropriate.

## Consequences

Connection Settings specification should be updated.

Go and .NET SDKs must modify the Connection Settings or Session Client constructor implementation to require a Client ID. Rust has no action, as it was already required there.

