# Terminology

The following outlines some of the main terms used to describe the different basic primitives used to construct the SDKs.

## Telemetry

Messages sent from a client such as a _device_ or an _asset_ to a given topic using a pre-defined schema, describable with [DTDL](https://github.com/Azure/opendigitaltwins-dtdl).

Described in detail in [telemetry-api.md](reference/telemetry-api.md).

## Command

Implement an RPC pattern, to decouple _clients_ and _servers_, where the client _invokes_ the command, and the server _executes_ the command, whether directly or by delegation.

Described in detail in [command-api.md](reference/command-api.md).

## Envoys and Binders

* A `Binder` is an abstraction to encapsulate the interactions between the application and the broker using a topic, schema and serialization format.
* An `Envoy` is a pair of `Binders` to represent both the Publisher and Subscriber components and how the interact.

Described in detail in [envoy-binder.md](reference/envoy-binder.md).

## Serializers

Binders require a serializer to convert between bytes and a consumable API. Serializers transform data, whether telemetry or commands, into the payload format and back again. Serializers implement an interface and are are used by libraries via dependency injection. Serializers are automatically generated via codegen, so they act on type safe schemas.

## Connection Management

As we are using dependency injection to initialize the Envoy and Binders, we need to provide the ability to react/recover to underlying connection disruptions.

Described in detail in [connection-management.md](reference/connection-management.md).

## Topic Structure

The topics used by the `Binder`s are defined in a topic template, composed by _tokens_ that will be replaced at runtime based on the values provided through the API. These tokens are described in _grammar_. Codegen may provide defaults for token substitution.

These topics will be compatible with and support [Unified Namespace (UNS)](https://www.linkedin.com/pulse/unified-namespace-driving-operational-excellence-from-phillips-mvp3c) requirements, as well as [OPC UA, Part 14: PubSub](https://reference.opcfoundation.org/Core/Part14/v105/docs/), and should enable other communication patterns such as Spark Plug B.

Described in detail in [topic-structure.md](reference/topic-structure.md).

## Message Metadata

Additionally to the defined topics, messages will include metadata properties to help with message ordering and flow control using timestamps based on the [Hybrid Logical Clock (HLC)](https://en.wikipedia.org/wiki/Logical_clock).

Described in detail in [message-metadata.md](reference/message-metadata.md).
