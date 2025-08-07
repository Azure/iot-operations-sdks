# Terminology

The following outlines some of the main terms used to describe the different basic primitives used to construct the SDKs.

## Envoys

Envoys are actors implementing our MQTT communication patterns which are currently RPC and telemetry.

### Telemetry

Messages sent from a client such as a _device_ or an _asset_ to a given topic using a pre-defined schema, describable with [DTDL](https://github.com/Azure/opendigitaltwins-dtdl).

<!--Described in detail in [telemetry.md](reference/telemetry.md).-->

### Commands

Implement an RPC pattern, to decouple _clients_ and _servers_, where the client _invokes_ the command, and the server _executes_ the command, whether directly or by delegation.

<!--Described in detail in [commands.md](reference/commands.md).-->

## Connection Management

As we are using dependency injection to initialize the Envoy and Binders, we need to provide the ability to react/recover to underlying connection disruptions.

<!--Described in detail in [connection-management.md](reference/connection-management.md).-->

## Message Metadata

Additionally to the defined topics, messages will include metadata properties to help with message ordering and flow control using timestamps based on the [Hybrid Logical Clock (HLC)](https://en.wikipedia.org/wiki/Logical_clock).

<!--Described in detail in [message-metadata.md](reference/message-metadata.md).-->
