# ADR 17: Error Refresh

## Status

PROPOSED

## Context

There are various aspects of our [error model][1] that we believe have not held
up to implementation considerations, but are too minor to each have their own
ADR. This ADR is to collect them in a single place and propose updates.

## Decision

-   Remove `HTTP status code` from the user-facing error objects. The current
    error model focuses on `error kind` as a more protocol-specific way to
    communicate this information, and including the HTTP concepts seems to just
    cause communication confusion.
-   Remove `in application`. With [`invocation error` removed][2] and a general
    push towards application errors being moved into the response body, this
    field has lost much of its meaning (and now only applies to
    `execution error`).
-   Allow all client-side errors to expose a `nested error` independent of
    `error kind`. This allows us to expose any dependency- or language-specific
    errors that may be relevant.

### Error Kinds

-   Remove `invalid argument`. This has heavy overlap with
    `invalid configuration` (which will largely replace it), and in some
    languages the distinction goes as far as being somewhat arbitrary. In
    addition, there are a number of cases where it interferes with code reuse.
-   Clarify the distinction of `invalid state`, `internal logic error`, and
    `unknown error`, and revisit usage in this context. The current thought is
    that the distinction relies primarily in the component at fault:
    -   `invalid state` - the user's code or system is at fault (e.g. clock
        skew; counter overflow should also likely become this).
    -   `internal logic error` - the SDK's code is at fault (should be fairly
        rare, but a Go example is "the MQTT client returned both no response and
        no error" which it should never do).
    -   `unknown error` - the error bubbled up from a dependency in a way the
        SDKs were otherwise not designed to handle.
-   Merge `unsupported request version` and `unsupported response version` into
    `unsupported version`. The source of the error (e.g. invoker or executor) is
    already communicated by `is remote` (or the equivalent), and the information
    communicated by the `error kind` is otherwise the same. The SDKs should
    still, however, distinguish these cases in the error message for ease of
    logging.

[1]: ../../reference/error-model.md
[2]: ./0015-remove-422-status-code.md
