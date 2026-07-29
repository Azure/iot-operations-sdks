# ADR 31: MQ Backpressure Bypass for mRPC Traffic

## Context

The MQ broker is adding a high-priority backpressure-bypass mechanism so
control-plane traffic (e.g., State Store) is not starved when data-plane
traffic fills the broker's buffer pool. The mark is an MQTT 5 **user
property on each PUBLISH** named `$high_priority` (name owned by the
broker). The property has no value — its presence alone signals
high priority. The broker also gets a CRD (Custom Resource Definition)
kill switch and an authorization (authz) policy gating who may set the
flag. No other MQTT semantics change: QoS, expiry, topic, correlation,
and cache behavior are all unaffected.

This is an MQTT-transport mechanism, not part of the AIO mRPC protocol
itself. The SDK achieves it by attaching an MQTT user property; nothing
in the mRPC contract or message schema changes.

## Decision

### mRPC

- **Always on for mRPC.** Every mRPC PUBLISH — both the invoker's
  request and the executor's response — carries `$high_priority` by
  default. The invoker and executor each set it on their own PUBLISH;
  the executor always marks its response and does not condition it on
  whether the request carried the flag.
- **No public option.** The SDK does not expose a way to turn the flag
  off or override it per call; mRPC traffic is always marked. Operators
  who need to claw the capability back use the broker-side controls (the
  authz policy and the CRD kill switch), not an SDK setting.
- **Abstracted from callers.** The flag is set inside the SDK's MQTT
  layer. It is not surfaced through the mRPC/protocol API and does not
  leak MQTT concepts into the higher-level surface.
- **SDK-shipped service clients use the default.** State Store, Lease
  Lock, Schema Registry, Azure Device Registry, and the connector
  framework inherit the always-on behavior like all other mRPC traffic;
  no override is exposed on their public surface.

### Wire

- The user property `$high_priority` is broker-owned and sits outside
  the SDK-reserved `__` prefix from
  [ADR 4](./0004-reserved-user-properties.md). SDKs must not validate
  against or reject `$`-prefixed user properties. If a user sets
  `$high_priority` themselves, the contents don't matter to the broker,
  so there is no concern about unexpected behavior.

### Codegen

- No DTDL annotation. Bypass is a property of the transport, not the
  contract. Generated invokers and executors behave the same as
  hand-written ones — always on, with no override surfaced — so codegen
  and non-codegen paths stay consistent.

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
- Because the flag is always on and not exposed, the SDK surface stays
  simple and consistent across languages, codegen, and service clients.
  Disabling or tuning the behavior is an operator concern handled by the
  broker's authz policy and CRD kill switch.
