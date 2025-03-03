# Shared Subscriptions

Shared subscriptions allow sharing of a single subscription, so that messages on a topic can be distributed across multiple clients. This allows load balancing with RPC invocations, as they can be performed against a service rather than one sole executor.

Many brokers do not support shared subscriptions, and therefore only allow one executor. Using more than one command executor cannot be done without shared subscriptions because an RPC request should only be responded to once.

## Usage

The topic structure of shared subscriptions is as follows:

`$share/<GROUPID>/<TOPIC>`

1. `$share`: The shared subscription identifier demarcates the topic as a shared subscription.
1. `<GROUPID>`: The group identifier, which identifies subscribers that will participate.
1. `<TOPIC>`: The published topic string.

Clients subscribe to the full topic structure (i.e. `$share/<GROUPID>/<TOPIC>`).
Clients publish messages to the topic filter (i.e. `<TOPIC>`).

## Why do we need to partition?

The partition ID is set as a user property on the PUBLISH, and is hashed by the MQTT Broker and used as the key to determine which subscriber within the shared subscription group receives this message. This ensures a cacheable publish will be routed to the same executor every time so the response is the same each time (will always reuse the same executors cache)

<!-- Mqtt Broker will send the publish to the subscriber based on the following logic:

|PUBLISH has `$partition`|SUBSCRIBE contains wildcard|Assignment based on|
|-|-|-|
|No|No|round-robin|
|No|Yes|consistent hashing of topic|
|Yes|-|consistent hashing of `$partition`| -->

## SDK Configuration

The `$partition` user property will be set on every `PUBLISH`. This will be done regardless of whether shared subscriptions are being used.

The SDK invoker sets the `$partition` user property to the **client ID**, and will always be required. Additional modes will be supported by the SDK in the future, based on customer demand and the implementation of the MQTT Broker.

Mqtt Broker uses the `$partition` user property as the hash key to determine which executor will receive the published message. This way, SDK invocations from a single invoker can be ensured to go to the same executor every time.

At this time, the `$partition` contents will not be configurable by the customer, however this will be reviewed for the future based on customer feedback.

## Future work items

1. Subscriber can configure executor assignment strategy
1. Customer configuration of `$partition` property
