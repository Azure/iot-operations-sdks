# ADR 16: Response Topic Pattern

## Context

From conversations with stakeholders, there are two primary requirements around
response topics:

-   The response topics themselves must be customizable at runtime (e.g. to add
    a random component to allow better parallelism and/or correlation).
-   Invokers must only require a single subscription (per invoker) to listen to
    their response topics.

## Decision

The SDKs will provide a "response topic pattern" string option for invokers to
allow full customization of the response topic. This will exist alongside the
current "response topic prefix/suffix" options, which will remain as a
convenience for customizing the response topic (that works well with codegen),
and the default behavior will continue to follow [ADR 14][2]. Invokers will
subscribe to this pattern akin to how executors behave per [ADR 1][1].

## Consequences

Languages whose codegen creates a one-to-many relationship between user-provided
options and invokers will not be able to support full customization in codegen
using the "response topic pattern" option (though the prefix/suffix options will
continue to work as before); this will require some form of additional mapping
at the codegen layer.

Since this concern does not apply to all languages, however, and it is not clear
that customization beyond prefix/suffix is an actual customer requirement, we
can leave this unresolved until/if that requirement changes.

[1]: ./0001-generalized-topic-tokens.md
[2]: ./0014-response-topic-prefix.md
