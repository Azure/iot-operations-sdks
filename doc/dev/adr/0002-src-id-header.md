# ADR 2: Source ID Header

## Status

APPROVED

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
* The TelemetryReceiver will not reqire the "__srcId" header to be present in a received Telemetry message.
* The TelemetrySender will no longer require that topic patterns contain the token `{senderId}`.
* If a "__srcId" header is present in a received Telemetry message, the TelemetryReceiver will extract the sender's ID from the "__srcId" header value and relay it to the user code that receives the Telemetry.
* The CommandInvoker will set the new "__srcId" header instead of the "__invId" header.
* The CommandExecutor will require the "__srcId" header to be present in a received Command request message.
* The CommandExecutor will read the value of the "__srcId" header instead of the "__invId" header when determining the requester ID to use for caching policy decisions.

## Protocol Transition

Because the AIO components are not yet in preview, this change will be made in a non-backward-compatible fashion.

However, to ease the transition to the revised protocol:

* The next revision of the TelemetryReceiver will:
  * First attempt to extract the sender's ID from the "__srcId" header if present.
  * Second, attempt to extract the sender's ID from the topic if the topic patterns contain the token `{senderId}`.
  * Third, continue without error if neither of the above two attempts is successful at determining  the sender's ID.
* The next revision of the TelemetrySender will have no special transitional behavior; it will set the "__srcId" header inTelemetry messages, per the above design.
* The next revision of the CommandInvoker will set BOTH the new "__srcId" header AND the old "__invId" header, using the same sender ID for the value.
* The next revision of the CommandExecutor will:
  * First, attemt to extract the the invoker's ID from the "__srcId" header if present.
  * Second, attempt to extract the the invoker's ID from the "__invId" header if present.
  * Third, return an error to the CommandInvoker if neither the "__srcId" nor the "__invId" header is preset in the Command request message.
