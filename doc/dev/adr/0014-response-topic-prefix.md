# ADR 14: Response Topic Prefix

## Status

APPROVED

## Context

The SDKs originally had a default response topic prefix of
`clients/{invokerClientId}` (using a well-known topic token). These were removed
as part of [ADR 1][1], since that topic token could no longer be assumed to
exist.

However, the only existing mechanism to secure topics (in this case, to ensure
that clients can only listen to their own response topics) is to configure
authorization rules on the broker (and, in this case, use a response topic that
is unique to the caller). This is only possible if the user knows the structure
of the response topic, meaning either they provided it or it is well-documented.
In order to lean towards a "secure by default" stance, we should provide a
well-documented default that the user can use or follow.

## Decision

The SDKs will re-introduce a default topic prefix. This prefix will have a value
of `clients/` followed by the client ID (manually constructed, since we cannot
assume a token of that value) to match the prefix [recommended by DSS][2]. This
prefix will be added iff the caller provides no other response topic options; it
is intended to provide a default behavior for the response topic as a whole,
rather than for the prefix specifically. We will publically document this
pattern for security configuration purposes.

## Open Questions

We may want to consider replacing the topic in the dedup cache key with the
response topic, for a closer conceptual match. Since both cases provide us auth
assurances, though, doing so likely doesn't provide any significant additional
security benefit.

[1]: ./0001-generalized-topic-tokens.md
[2]:
    https://learn.microsoft.com/en-us/azure/iot-operations/create-edge-apps/concept-about-state-store-protocol#state-store-system-topic-qos-and-required-mqtt5-properties
