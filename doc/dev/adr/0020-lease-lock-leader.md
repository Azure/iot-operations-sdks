# ADR 20: Lease, Lock, & Leader

## Context

As originally presented, leased-lock/leader-election had two components
(leased-lock, which contained a single-attempt acquire, and leader-election,
which contained a multi-attempt campaign). However, as implemented, the two
components changed scope; the leased-lock library implemented both
single-attempt and multi-attempt, and the leader-election library (currently
.NET only) became simply a renamed duplicate. There is concern that the current
state of the libraries does not accurately represent what functionality we are
supporting and/or does not fully cover all customer scenarios.

The problem this ADR is attempting to solve is primarily one of terminology and
organization. Its goal is twofold:

-   Clearly distinguish the lower- and higher-level constructs being exposed.
-   Clearly indicate that the currently-implemented higher-level construct
    behaves more accurately like a mutex than full leader election.

While there is missing functionality in the leader-election space (which the
`Leader` section covers some of), the actual design of that is largely outside
of the scope of this ADR.

## Decision

The general proposal which follows is to return to the _structure_ of the
original presentation while favoring the _terminology_ of the current
implementation. It splits LL/LE into three components (likely three
classes/structs grouped together):

-   `Lease`
    -   The primitive construct supported by DSS.
    -   Primarily intended to be used to build higher-level constructs, but can
        be used directly for more complicated scenarios or higher-level
        scenarios that are not currently supported.
    -   Should be exposed directly by the higher-level objects by whatever
        mechanism is most convenient for the language (e.g. inheritance in C#,
        struct-embedding in Go, etc.).
    -   Aliasing should be limited to make it clear which calls map to the
        lower-level and higher-level constructs.
    -   Exposes the following operations:
        -   `acquire: (lease: duration, [options]) => bool`: Make a single
            attempt to acquire the lease with a given lease duration. Returns a
            boolean indicating whether or not this succeeded (or an error as
            appropriate per language semantics). Supports the following options:
            -   `timeout: duration`: Execution timeout for this attempt;
                primarily a passthrough to underlying DSS APIs.
            -   `renew: duration`: Indicate that this lock should be
                automatically reacquired at the given interval.
        -   `release: () =>`: Releases the lease.
        -   `observe`: Watch for changes in the lease holder. Exact semantics
            are language-specific and should generally follow the same pattern
            as `KEYNOTIFY`.
        -   `get token: () => hlc`: Get the most recent fencing token provided
            by the lease; if the most recent attempt failed (e.g. during
            autorenew), this call should fail with the same error. Note that
            this is separate from the `acquire` call for two reasons: because
            the token may change spontaneously due to autorenew, and to clarify
            that the overall synchronization construct is not DSS-specific.
        -   `get holder: () => ?string`: Get the current holder of the lease, or
            a clear indicator that there is none (e.g. an `Option` in Rust, a
            secondary boolean return in Go, etc.).
-   `Lock`
    -   Provides mutex-like semantics, where the protected user code _follows_ a
        single blocking call.
    -   Intended to be used for:
        -   Exclusive access to shared resources. Note that while the strongest
            guarantees exist for DSS entries (via the `get token` call exposed
            from the underlying `Lease`), this construct is intended to support
            any shared resource.
        -   Active/passive replication where the passive replicas do nothing but
            wait to become active.
    -   Exposes the following (additional) operations:
        -   `lock: (lease: duration, [options]) =>`: Block until the lease/lock
            is acquired (or the operation times out/is cancelled). Implemented
            using an `acquire`/`observe` loop. Supports the following option in
            addition to the `acquire` options:
            -   `delay: retry policy`: A delay/backoff following the state
                changing to prevent crowding/lock contention.
        -   `unlock: () =>`: Alias for `release` to provide semantic parity with
            `lock`.
-   `Leader`
    -   Provides run-loop/state-machine semantics, where the protected user code
        is _contained_ within the loop (and is built to react to the state
        changing).
    -   Intended to be used for:
        -   Primary/subordinate topologies.
        -   Active/passive replication where the passive replicas must take
            distinct action.
    -   API TBD, but should generally be built around an [`acquire` -> take
        action (for either result) -> `observe`] loop.

For public preview, we will ship the `Lease` and `Lock` components, since they
have been implemented in all languages (modulo splitting into the two parts) and
cover the bulk of the original feature as presented. `Leader` will be shipped as
a follow-up once we have had the opportunity to design its API structure.

## Open Questions

-   Is exponential backoff the correct pattern to use for crowding prevention?
    Typically exponential backoff is used to break up a tight loop, but since
    `lock` (and the Leader semantics) already rely on `observe` to react only
    when the state changes, a simple static delay (with jitter) may be more
    appropriate. For now we should implement it for consistency across
    languages, but we'll leave this question open to possibly revisit in the
    future.
