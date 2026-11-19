# Authorizing mRPC (Request/Response) over the MQTT Broker

This document describes how to author Azure IoT Operations MQTT Broker authorization
policies for the mRPC scenarios. The policies are required to ensure the following:

* an **executor** (subscriber) can only *receive* requests that are meant for it, and
* only the **invoker** that issued a request can *receive* the corresponding response.

It assumes the clients are K8s applications that authenticate to the MQTT
Broker with **K8s Service Account Token (SAT)** auth (`SatAuthFile` / `AIO_SAT_FILE`,
see [Connection Settings](connection-settings.md)).

For the underlying message flow see [RPC Protocol](rpc-protocol.md), for topics
see [Topic Structure](topic-structure.md), and for the policy schema see the
[AIO MQTT Broker authorization](https://learn.microsoft.com/en-us/azure/iot-operations/manage-mqtt-broker/howto-configure-authorization?tabs=portal) doc.

## Why mRPC is a special case

In ordinary MQTT pub/sub the publisher does not care which subscriber receives a
message — any authorized subscriber may. mRPC breaks both of those assumptions:

1. A single logical call spans **two correlated topics** — a request topic and a
   response topic.
2. The publisher **intends a specific recipient**: a request is meant for *one*
   executor (service); a response is meant for *the one* invoker that issued it.

Therefore authorization must do something pub/sub normally doesn't: **bind each
topic to the identity of the single client allowed to receive on it.** The lever
for that is restricting the `Subscribe` action — not just `Publish` — per
principal, using per-identity topic segments.

## Topic shapes

Place the routing identity in its own path segment; broker token substitution
requires a token to be the *entire* segment (e.g. `commands/{executorId}/+` is
valid, `command-{executorId}/+` is not).

* Request topic (executor-keyed):

  ```text
  commands/{executorId}/{commandName}
  ```

* Response topic (invoker-keyed), using the default response prefix from
  [ADR 14](../dev/adr/0014-response-topic-prefix.md):

  ```text
  clients/{invokerClientId}/commands/{executorId}/{commandName}
  ```

The invoker learns nothing about the executor's MQTT client ID, and the executor
learns the response topic at runtime from the `ResponseTopic` property of the
request message. The only identities that appear in the topics are:

* `{executorId}` — the **stable, pre-known service identity** of the executor.
* `{invokerClientId}` — the invoker's own client ID, which the invoker always knows.

---

## Option A (preferred) — Executor identified by a pre-known `executorId` (well-known topic)

> [!NOTE]
> This is the recommended model. Use it when the invoker does not (and should not)
> know the executor's MQTT client ID, and/or when the executor is horizontally
> scaled.

The executor is addressed by a **stable, operator-assigned service identity**
(`executorId`) that is *decoupled from the MQTT client ID*. The invoker publishes
to a well-known topic such as `commands/temperature-service/getStatus` without
knowing which pod (client ID) will handle it.

The `executorId` is asserted by the broker from the **SAT-derived attributes** of
the executor's service account, so it cannot be spoofed by the client. The
operator assigns the attribute when configuring authentication (e.g. an
`executorId` attribute on the executor's service account principal).

```yaml
apiVersion: mqttbroker.iotoperations.azure.com/v2beta1
kind: BrokerAuthorization
metadata:
  name: mrpc-authz-by-executorid
  namespace: azure-iot-operations
spec:
  listenerRef:
    - listener
  authorizationPolicies:
    cache: Enabled
    rules:
      # ---- Executors (one rule covers every replica of every service) ----
      - principals:
          attributes:
            - role: "rpc-executor"
        brokerResources:
          - method: Connect
          - method: Subscribe
            topics:
              # Receive ONLY requests addressed to this service's pre-known id.
              # `executorId` comes from the authenticated SAT identity, not the client ID.
              - "commands/{principal.attributes.executorId}/+"
          - method: Publish
            topics:
              # Reply to any invoker, but only for commands targeting this service.
              - "clients/+/commands/{principal.attributes.executorId}/#"
      # ---- Invokers ----
      - principals:
          attributes:
            - role: "rpc-invoker"
        brokerResources:
          - method: Connect
          - method: Publish
            topics:
              - "commands/+/+"                       # send requests to any service
          - method: Subscribe
            topics:
              - "clients/{principal.clientId}/#"     # receive only own responses
```

It is also possible to hardcode the identity and the topics. While less flexible, this is a secure approach as the receive scope is a constant tied to a verified identity and cannot be widened by a spoofed client ID:

```yaml
      - principals:
          attributes:
            - serviceAccountName: "temperature-service-sa"   # unspoofable, from SAT
        brokerResources:
          - method: Connect
          - method: Subscribe
            topics: [ "commands/temperature-service/+" ]
          - method: Publish
            topics: [ "clients/+/commands/temperature-service/#" ]
```

### Horizontally scaled executors (shared subscriptions)

Multiple executor replicas that serve the same `executorId` share a single
subscription so that each request is handled once
(see [Shared Subscriptions](shared-subscriptions.md)). Replicas subscribe to:

```text
$share/<group>/commands/{executorId}/+
```

Authorization is evaluated against the **underlying topic filter**
(`commands/{executorId}/+`), so the same `Subscribe` rule above already covers the
shared subscription — no extra rule is required. Two configuration points matter:

* The executor principals **must be matched by the SAT attribute, not by client
  ID**, so that every replica (each with a distinct client ID) matches the one
  rule.
* The invoker sets the `$partition` user property to its client ID, so a given
  invoker's calls are routed to the same replica for cache affinity. `$partition`
  does not affect authorization (which is evaluated on the topic).

---

## Option B — Executor identified by its MQTT client ID

In this option, the invoker needs to target a specific executor by using the MQTT client id of the executor. In most cases, the invoker does not know this information. So, for most scenarios, Option A should be preferred. The authorization for this scenario is included here for completeness purposes.

Here `{executorId}` resolves to the executor's MQTT client ID, and the executor
scopes its subscription with `{principal.clientId}`.

```yaml
apiVersion: mqttbroker.iotoperations.azure.com/v2beta1
kind: BrokerAuthorization
metadata:
  name: mrpc-authz-by-clientid
  namespace: azure-iot-operations
spec:
  listenerRef:
    - listener
  authorizationPolicies:
    cache: Enabled
    rules:
      # ---- Executors ----
      - principals:
          attributes:
            - role: "rpc-executor"
        brokerResources:
          - method: Connect
          - method: Subscribe
            topics:
              - "commands/{principal.clientId}/+"          # receive only own requests
          - method: Publish
            topics:
              - "clients/+/commands/{principal.clientId}/#" # reply only for own commands
      # ---- Invokers ----
      - principals:
          attributes:
            - role: "rpc-invoker"
        brokerResources:
          - method: Connect
          - method: Publish
            topics:
              - "commands/+/+"                              # send requests to any executor
          - method: Subscribe
            topics:
              - "clients/{principal.clientId}/#"            # receive only own responses
```

> [!IMPORTANT]
> `{principal.clientId}` substitution is only safe if a client cannot connect with
> another client's ID. With SAT auth you must bind the client ID to the
> authenticated identity — e.g. constrain the allowed `clientIds` on the principal
> / `Connect` resource so a service account may only connect under its expected
> client ID. Without this binding, a malicious executor could connect with a
> victim's client ID and receive its requests.

### Gap in Option B

The invoker must know the executor's MQTT client ID **ahead of time** to address
the request. Client IDs are often dynamic (per pod, per restart) and there may be
**multiple executor replicas** behind one logical service, each with a different
client ID. Option A removes this requirement.

---

## Topic restriction summary

| Role | Action | Topic restriction | Purpose |
|------|--------|-------------------|---------|
| Executor | `Subscribe` | `commands/{principal.attributes.executorId}/+` | Receive only requests addressed to its pre-known service id |
| Executor | `Publish` | `clients/+/commands/{principal.attributes.executorId}/#` | Reply to any invoker, only for commands targeting itself |
| Invoker | `Subscribe` | `clients/{principal.clientId}/#` | Receive only its own responses |
| Invoker | `Publish` | `commands/+/+` | Send requests to any executor (narrow if desired) |

The two security-critical lines are the `Subscribe` rules — they make a request
reachable only by its intended executor and a response reachable only by its
intended invoker. The `Publish` restrictions are defense-in-depth: they stop a
compromised executor from forging responses that appear to come from a *different*
service.

Token-substitution validity (token must be the whole segment):

* `commands/{principal.attributes.executorId}/+` ✓
* `clients/+/commands/{principal.attributes.executorId}/#` ✓
* `clients/{principal.clientId}/#` ✓

---

## Bridging the security and configuration gaps

| Gap | Risk | How this design closes it |
|-----|------|---------------------------|
| Invoker does not know the executor's client ID | Cannot address the request; client-ID token substitution unusable | Address by a stable `executorId` carried in the request topic; the executor scopes its subscription with the SAT-derived `executorId` attribute |
| `executorId` could be spoofed | A rogue service subscribes to another service's request topic | The `executorId` (or `serviceAccountName`) comes from the **authenticated SAT identity** asserted by the broker, not from a client-supplied value; the operator controls attribute assignment |
| Multiple executor replicas / restarts | Per-pod client IDs change; per-client-ID rules don't fit | Match executor principals on the **attribute** (not client ID) and use **shared subscriptions**; one rule covers all replicas |
| Client ID spoofing (Option B) | Executor receives another's requests | Bind the client ID to the identity via `clientIds` on the principal / `Connect`, or use Option A (`executorId` attribute, optionally the explicit fixed-string variant) |
| Cross-service response forgery | Compromised executor publishes fake responses for another service | Executor `Publish` is scoped to `.../commands/{its own executorId}/#` |
| Invoker needs to know the response topic | Cannot secure its own responses | Default response prefix `clients/{invokerClientId}/...` ([ADR 14](../dev/adr/0014-response-topic-prefix.md)); invoker subscribes only to `clients/{principal.clientId}/#`, which it always knows |

---

## Why this is the secure way for the clients to communicate

* **Least privilege on the receive path.** Each client's `Subscribe` is scoped to
  a single identity segment (`commands/<service>/...` for executors,
  `clients/<self>/...` for invokers). A client cannot subscribe to another
  client's request or response topic, so messages reach only their intended
  recipient — the guarantee mRPC needs and that plain pub/sub does not provide.
* **Identity is authenticated, not asserted.** Executor principals are matched on
  SAT-derived attributes (service account / `executorId`), and invoker scoping
  rests on the mandatory client ID ([ADR 18](../dev/adr/0018-mandatory-client-id.md)).
  The topic ACLs rest on the Kubernetes identity the broker verified, not on
  values an application supplies in a packet.
* **Stable addressing without leaking topology.** Invokers target a well-known
  service identity and never need the executor's client ID, so executors can scale,
  restart, and move freely without any change to invoker configuration or to the
  authorization policy.
* **No shared or wildcard listening.** Nothing grants `Subscribe` on `#` or broad
  prefixes. Compromise of one executor or invoker cannot be used to eavesdrop on
  traffic destined for others; the blast radius is one identity.
* **Forgery resistance on the send path.** Executors may only publish responses on
  `.../commands/{their own executorId}/#`, so a compromised executor cannot
  impersonate another service's responses.
* **Routing and confidentiality from one rule.** Because the response topic embeds
  the invoker's client ID, responses are both correctly routed and confidential by
  construction — the broker-evaluated rule delivers routing and authorization with
  no separate trust step.
* **Centralized authorization in the broker.** The broker authorization cache keeps strict per-client
  checks inexpensive, so security does not trade off against throughput.
