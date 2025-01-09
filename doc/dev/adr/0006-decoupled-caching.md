# ADR 6: Decoupled Caching

## Status

APPROVED

## Context

Caching is used for two distinct purposes in the protocol (described in more
detail [here](../../reference/command-cache.md)). Roughly, these two purposes
are:

-   Deduplicating messages that have been repeated by the MQTT broker for
    whatever reason. This is relatively simple, and necessary for protocol
    correctness.
-   Reusing idempotent requests for efficiency/performance. This has the
    potential to be highly handler-implementation specific, and is largely
    optional.

The current cache implementations handle both of these purposes in a single,
fairly complex cache. In addition, very few options are exposed for caching
behavior - currently only whether a command is itempotent and, if so, what its
cache TTL should be - which highly limits the specificity of the second purpose.

## Decision

The two caching use-cases should be split into two distinct caches:

-   The deduplication cache, as it is required for correctness, should be
    maintained by the protocol.
    -   For simplicity and uniformity, requests should never be evicted from
        this cache until they expire.
        -   Given this, and the fact that requests are specific to a command,
            this cache does not need to be shared between executors.
        -   This also has the side-effect of eliminating idempotency as an
            innate concern of the protocol.
    -   The keys for this cache should be updated to be topic+correlation
        instead of clientID+correlation, since the broker can enforce auth on
        the topic but not the user-properties used to transmit the clientID.
    -   This cache should operate at the (fully-resolved) MQTT message level, to
        ensure that the responses to duplicate requests are identical.
-   The reuse cache should be entirely user-provided (with possible standard
    implementations provided by the SDK).
    -   This allows both easy sharing of this cache between executors as well as
        a high degree of customizability, from details like equivalency checking
        to custom storage solutions.
    -   In addition, even for SDK-provided helpers, customization/options are
        cleanly separated from the executor.

For GA, the reuse cache will be removed from the SDK and will rely purely on
user implementation in the handler.

## Open Questions

> [!NOTE]  
> With the reuse cache being removed from the SDK for GA, these open questions
> are no longer immediately relevant. They're being left in this document for
> future consideration when/if we elect to revisit reuse caching in the SDK.

The primary open question concerns at what level the reuse cache operates. There
are two primary options that have both advantages and disadvantages.

-   At the MQTT message level
    -   Advantages:
        -   This is closest to the current behavior, and allows us to provide a
            cache helper that operates largely the same.
        -   Computing factors like message size is relatively straightforward on
            serialized payloads.
        -   Operating prior to parsing and after serialization allows the cache
            to avoid these operations.
    -   Disadvantages:
        -   This requires exposing a significant amount of the underlying MQTT
            structure to the user, breaking a level of abstraction provided by
            the protocol library.
        -   Since this by-definition operates outside of the handler
            implementation, it requires explicit hooks into the protocol's
            operation.
-   At the handler level
    -   Advantages:
        -   The information that needs to be exposed to the cache is identical
            to that exposed to the handler.
        -   This can operate as a helper or wrapper for the handler itself, and
            doesn't require additional options on the executor.
        -   Comparing parsed values can take into account factors like key
            ordering that shouldn't affect equivalency but may affect the
            serialized result.
        -   This behaves largely the same as the user implementing caching in
            their own handlers, without the SDK's involvement at all.
        -   It allows for calling the cache within the handler (instead of as a
            wrapper of the handler), which unlocks more complex user scenarios.
    -   Disadvantages:
        -   Computing factors like byte sizes on parsed types is problematic,
            making reimplementing existing behavior (cost weighted benefit,
            etc.) difficult if not impossible without intervention by the user.
        -   Parsing and serialization will occur even on cache hits.
