# ADR8: Protocol Split

## Context

RPC Command and Telemetry are considered the same protocol with a single shared version and all-encompassing error design.
However, as these two patterns are distinct and customers may only be concerned with one of the two patterns, this unified version creates problems for customer communication - changes to one pattern would change the shared version, even if the other pattern went unchanged. Furthermore, the shared error design can expose customers to fields that are irrelevant to the particular pattern they may be using. All of these issues are exacerbated if additional patterns are added.

This ADR iterates upon [ADR 0003](./0003-protocol-versioning-0.1).

## Decision:

Versioning for the wire protocol should be split into two independent versions for:

* RPC Command
    * `__protVer` property in [Command request messages](../../reference/message-metadata.md#request-message) refers to the Command protocol version
    * `__protVer`, `__supPortMajVer` and `__requestProtVer` properties in [Command response messages](../../reference/message-metadata.md#response-message) refer to the Command protocol version
    * Note that both request and response share a protocol version that governs their behavior

* Telemetry
    * `__protVer` property in [Telemetry messages](../../references/message-metadata.md#telemetry-message) refers to the Telemetry protocol version

Both of these versions should start at the current unified version in use (`1.0`), and iterate independently from there. From here on out, each of RPC Command and Telemetry, as well as any future patterns are each considered to be an independent protocol

This does **not** affect the package versioning for the SDK, as there will still be a single package. This only affects the wire protocol version.
