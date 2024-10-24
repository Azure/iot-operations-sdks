# ADR 2: Source ID Header

## Status

PROPOSED

## Context

Removing knowledge of specific tokens from the SDKs (per [ADR 1](./0001-generalized-topic-tokens.md)) has an important consequence:
It is no longer feasible for the TelemetryReceiver to extract the sender ID from the publication topic, because the `{senderId}` token can no longer be required per the defined [Topic Structure](../../reference/topic-structure.md).

An analogous situation has previously arisen:
The CommandExecutor became unable to extract the invoker ID from the request topic.
This was addressed by changing the protocol to add an MQTT header "__invId" to carry this information instead of relying on its presence at a defined level in the topic.

Although the header name "__invId" suggests that it identifies an invoker, the identifier value does not have any aspects that are specific to a CommandInvoker.
Rather, the value is an identifier of the source of the message, whether that source is a CommandInvoker, a TelemetrySender, or something else.

## Decision

A new [MQTT header](../../reference/message-metadata.md) will be added to the protocol:

* The the header name will be "__srcId".
* The header value will be an identifier of the message sender.

The new header will be used as follows:

* The TelemetrySender will set the "__srcId" header in all Telemetry messages; the TelemetrySender's ID will be the header value.
* The TelemetrySender will no longer require that topic patterns contain the token `{senderId}`.
* The TelemeetryReceiver will extract the sender's ID from the "__srcId" header value and relay it to the user code that receives the Telemetry.
* The CommandInvoker will set the new "__srcId" header instead of the "__invId" header.
* The CommandExecutor will read the value of the "__srcId" header instead of the "__invId" header when determining the requester ID to use for caching policy decisions.

## Protocol Versioning

In accordance with the design of [Protocol Versioning](../../reference/protocol-versioning.md), this change will bump the major version number from 1 to 2, since it is a breaking change.

To ease the transition to the new protocol version, a minor-version change is also defined.
In protocol version 1.1:

* The TelemetrySender will set the "__srcId" header in all Telemetry messages BUT WILL CONTUNUE TO require that topic patterns contain the token `{senderId}`.
* The TelemeetryReceiver will extract the sender's ID from the "__srcId" header if present, BUT WILL FALL BACK TO extracting from the topic if the new header is not present.
* The CommandInvoker will set the new "__srcId" header IN ADDITION TO the "__invId" header.
* The CommandExecutor will read the value of the "__srcId" header instead if present, BUT WILL FALL BACU TO the "__invId" header if the new header is not present.
