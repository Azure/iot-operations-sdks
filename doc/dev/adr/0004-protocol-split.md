# ADR4: Protocol Split

## Status:

PROPOSED

## Context

RPC Command and Telemetry are considered the same protocol with a single shared version and all-encompassing error design.
However, as these two patterns are distinct and customers may only be concerned with one of the two patterns, this unified version creates problems for customer communication - changes to one pattern would change the shared version, even if the other pattern went unchanged. Furthermore, the shared error design can expose customers to fields that are irrelevant to the particular pattern they may be using. All of these issues are exacerbated if additional patterns are added.

This ADR iterates upon [ADR 0004](./0004-protocol-split.md).

## Decision:

Versioning for the wire protocol should be split into two independent versions:
    - RPC Command
    - Telemetry

Both of these versions should start at the current unified version in use (`0.1`), and iterate independently from there. From here on out, each of RPC Command and Telemetry, as well as any future patterns are each considered to be an independent protocol

This does **not** affect the package versioning for the SDK, as there will still be a single package. This only affects the wire protocol version.

Additionally, SDK languages are free to implement an idiomatic error design that can be unique to the particular protocol in question, rather than enforcing a single error type. Language in the [error model specification](../../reference/error-model.md) will be updated to reflect this, however the information provided by the error in any given case outlined in the specification **will not** change.