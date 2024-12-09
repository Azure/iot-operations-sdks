# ADR n: Differentiating Leader Election from Leased Lock

## Status

PROPOSED

## Context

The implementation of the Leader Election (LE) client (currently .NET only) is
functionally a copy of the Leased Lock client with different names. The general
consensus is that this is redundant; however, stakeholders have also indicated
that more focused LE semantics are still desirable. There is therefore a need to
reconsider the LE client API and determine how to make it best fit the customer
use-case. The design for LE should include:

-   The LE semantics should be independent from LL - e.g. even if LL is used for
    the implementation, the APIs should not be coupled.
-   LE should support the following:
    -   Election management (start, vote, and announce leader)
    -   Leader/Quorum management (get who is the leader, is it still the leader,
        who are the followers, register a new entity/participant, or remove an
        entity/participant)
    -   Health checking/failover

Possible options for implementation:

-   Still have an independent LE client, but abstract the underlying LL more.
-   If there is functionality in LL that is really more specific to LE, consider
    extracting it into the LE client only (to maintain a clearer separation of
    concerns).
-   Alternatively, keep the LE functionality in the LL client, but make its
    usage clearer for LE purposes.
