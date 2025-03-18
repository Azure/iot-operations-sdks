# ADR9: Protocol Package Error Structure Revisions

## Context

As of [ADR 0008](./0008-protocol-split.md), Telemetry, RPC Command and any other future patterns will each have their own unique protocol version on the wire, and should each be considered a distinct protocol.

However, the [error model specification](../../reference/error-model.md) still states that there should be a single error type for the entire `protocol` package. While this may be acceptable in some language implementations, in others this may present a problem for dealing with the semantics of decoupled Telemetry and RPC Command protocols. Additionally, there is a scalability problem as not all fields of the single error are relevant to all protocols, and this will only become more pronounced if additional protocols/patterns are added. Lastly, it has been raised that the use of a rigid `is_remote` boolean field to indicate whether an error was local or remote may not be idiomatic in all implementations.

## Decision

The specification will be updated to loosen these rigid requirements, or clarify the intent of existing ones that were unclear, in order to allow, but not require the following:

1) Multiple errors for the `protocol` package as necessary
2) The ability to remove irrelevant fields in an error representation
3) `is_remote` (and potentially other fields in future) to be represented by other means (e.g. error subtypes, enums, etc.)

What will **not** change is the information required to be provided to the end use by an error, or the situations in which that information is provided. This ADR merely allows for more flexibility in how the same information is communicated.