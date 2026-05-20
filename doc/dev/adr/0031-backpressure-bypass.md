# ADR 31: MQ Backpressure Bypass for mRPC Traffic

## Context

The MQ broker is adding a high-priority backpressure-bypass mechanism so
control-plane traffic (e.g., State Store) is not starved when data-plane
traffic fills the broker's buffer pool. The mark is an MQTT 5 **user
property on each PUBLISH** named `$high_priority` (name owned by the
broker). The property has no value — its presence alone signals
high priority. The broker also gets a CRD kill switch and an authz
policy gating who may set the flag. No other MQTT semantics change:
QoS, expiry, topic, correlation, and cache behavior are all unaffected.

## Decision

### mRPC

- **Invoker and Executor set the flag independently.**
  Each side decides per its own logic whether to attach `$high_priority`
  to the PUBLISH it produces. Request and response priorities are decoupled.
- **Granularity is per-PUBLISH.**
  Default behavior is to mark all PUBLISH with the flag, but this can be overridden per PUBLISH.

### Codegen

- No DTDL annotation. Bypass is a property of the caller, not the
  contract.

### Compatibility

- No protocol-version bump. Brokers that don't recognize the property
  (or have the kill switch on) treat it as opaque — safe fallback
  to normal-priority backpressure.
- Existing SDK consumers will see their outgoing mRPC traffic marked
  `$high_priority` after upgrading. This is intentional and aligned
  with the MQ ADR.

## Consequences

- mRPC traffic gets backpressure-bypass treatment by default, matching
  the MQ ADR's expectation and protecting first-party control-plane
  callers without requiring per-call opt-in.
- Symmetric invoker/executor options let operators tune each leg
  independently (e.g., mark requests, but not responses, or vice versa),
  so request and response priorities can diverge by design.
