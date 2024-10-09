# ADR 1: Generalized Topic Tokens

## Status

APPROVED

## Context

The current well-known topic tokens are not necessarily applicable to the topic
structure of existing services, most notably the [MQTT broker state store
protocol][1]. Having them integrated into the protocol SDKs limits the SDKs'
ability to interface with services that were not also implemented using the
protocol SDKs.

## Decision

Topic patterns will be generalized at the protocol SDK level, allowing them to
function with existing external MQTT topics. They will be structured as follows:

-   A **topic pattern** is a sequence of labels separated by `/`
-   Each label is one of:
    -   A string of printable ASCII characters not including space, `"`, `+`,
        `#`, `{`, `}`, or `/`
    -   A **token** which takes the form `{NAME}`, where the string "NAME" is a
        documentation stand-in for a specific token name which follows the same
        character rules as above (e.g. `{clientId}`)
-   The first label must not start with `$`

Topic patterns will be used in all of the protocol constructors in order to
generate the final MQTT topic names and filters used by the SDK. The tokens in
the patterns will be utilized as follows:

-   A map of token values may be provided to all constructors for tokens that
    are not necessarily known at compile time but are constant for the life of
    the envoy (e.g. the client ID). These values will be substituted for their
    tokens before any other processing (e.g. they will not be turned into
    wildcards).
-   For senders/invokers, any token values not provided to the constructor may
    be provided at runtime to the send/invoke method. These token values will be
    similarly substituted into the pattern in order to generate the actual topic
    name (and response topic name, if applicable) used in the MQTT publish. If a
    topic name contains any remaining tokens that have not been substituted at
    time of publish, it should be considered user error.
-   For receivers/executors, any tokens not provided to the constructor will be
    turned into MQTT `+` wildcards to generate the MQTT topic filter used in the
    subscription. When an MQTT publish is received, the receiver will parse the
    incoming topic name in order to extract a map of resolved token values to
    provide to the handler (which should include the tokens provided to the
    constructor for user convenience).
-   All token values must be a single label, as described above.
-   Providing values for tokens not present in a topic pattern is _not_
    considered an error. This allows consumers like the protocol compiler to
    provide well-known token values without needing to parse the pattern.

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
-   Topic patterns no longer require particular tokens (e.g. `{senderId}` in
    telemetry topics).

## Open Questions

-   The current definition of a topic label (adapted from the prior
    specification) is still more restrictive than the MQTTv5 topic spec, which
    allows effectively any UTF8 string outside of the three control characters
    (`/`, `+`, and `#`). Do we want to loosten our definition to support this?
-   Do we want to include common/recommended topic tokens (e.g. `{clientId}`) as
    defaults that the library provides (but can be overridden)?
-   The prior specification uses the prefix `ex:` to distinguish user-provided
    tokens, where the user-provided value map only includes the portion of the
    names following the prefix. Should this be supported at the protocol level,
    and if so, how (which may be language-specific)?

## References

This pattern aligns with the "URL parameter" concept found in many HTTP
frameworks (e.g. [express][2] or [axum][3]).

[1]:
    https://learn.microsoft.com/azure/iot-operations/create-edge-apps/concept-about-state-store-protocol
[2]: https://expressjs.com/guide/routing.html
[3]: https://docs.rs/axum/latest/axum/struct.Router.html#captures
