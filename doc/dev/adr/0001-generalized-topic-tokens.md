# ADR 1: Generalized Topic Tokens

## Status

PROPOSED

## Context

The current well-known topic tokens ([internal document][1]) are not necessarily
applicable to the topic structure of existing services, most notably the [MQTT
broker state store protocol][2]. Having them integrated into the protocol SDKs
limits the SDKs' ability to interface with services that were not also
implemented using the protocol SDKs.

## Decision

Topic patterns will be generalized at the protocol SDK level, allowing them to
function with existing external MQTT topics. They will be structured as follows:

-   A topic pattern is a sequence of labels separated by `/`
-   Each label is one of:
    -   A string of printable ASCII characters not including space, `"`, `+`,
        `#`, `{`, `}`, or `/`
    -   A token which takes the form `{name}`, where the token name follows the
        same character rules as above
-   The first label must not start with `$`

Topic patterns will be used in all of the protocol constructors in order to
generate the final MQTT topics and topic filters used by the SDK. The tokens in
the patterns will be utilized as follows:

-   All token values must be a single label, as described above.
-   A map of token values may be provided to all constructors for tokens that
    are not necessarily known at compile time but are constant for the life of
    the envoy (e.g. the client ID). These tokens will always be static in the
    resulting topics/filters (e.g. they will not be turned into wildcards).
-   For senders/invokers, a map of token values may be provided to the send call
    for fully dynamic tokens. These token values will be substituted into the
    pattern in order to generate the actual topic used in the MQTT publish; if
    any unresolved tokens remain after substitution, this should be considered
    user error.
-   For receivers/executors, any tokens not provided to the constructor will be
    turned into MQTT `+` wildcards to generate the MQTT topic filter used in the
    subscription. When an MQTT publish is received, the receiver will parse the
    incoming topic in order to extract a map of resolved token values to provide
    to the handler (which should include the tokens provided to the constructor
    for user convenience).

Libraries which wrap the protocol SDKs (e.g. the protocol compiler and service
libraries) may still provide and/or require well-known tokens, since they are
built to communicate with known endpoints.

## Consequences

-   More logic is moved to the protocol compiler. While this does centralize a
    lot more of the understanding, it also increases its complexity.
-   Common patterns (e.g. `{clientId}`) may require more boilerplate to use.
-   Passing token values as maps instead of arguments sacrifices ergonomics for
    flexibility, though this will be mitigated somewhat at the protocol compiler
    level.
-   Behavioral differences dependent on the presence or absence of particular
    topic tokens (e.g. caching decisions based on `{executorId}`) do not mesh
    well with this design and will need to be reconsidered.

## Open Questions

-   The current definition of a topic label (adapted from the prior
    specification) is still more restrictive than the MQTTv5 topic spec, which
    allows effectively any UTF8 string outside of the three control characters
    (`/`, `+`, and `#`). Do we want to loosten our definition to support this?
-   Do we want to include common/recommended topic tokens (e.g. `{clientId}`) as
    defaults that the library provides (but can be overridden)?

## References

This pattern aligns with the "URL parameter" concept found in many HTTP
frameworks (e.g. [express][3] or [axum][4]).

[1]:
    https://github.com/microsoft/mqtt-patterns/blob/main/docs/specs/topic-structure.md
[2]:
    https://learn.microsoft.com/en-us/azure/iot-operations/create-edge-apps/concept-about-state-store-protocol
[3]: https://expressjs.com/en/guide/routing.html
[4]: https://docs.rs/axum/latest/axum/struct.Router.html#captures
