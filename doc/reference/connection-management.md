# Connection Management

## Introduction

The MQTT protocol is designed to improve the basic TCP communication by adding control flow semantics (QoS/PUBACK) that can help application developers to recover from TCP connection issues.

In MQTT, the connection is managed by the KeepAlive packet, and is orthogonal to the PUB/SUB operations. SUBS can use persistent sessions to be recovered from connection failures, however PUB operations require some policies to decide what to do when the connection was not present.

As we are embracing Dependency Injection in the Envoy/Binder pattern, we need to find a way to support different MQTT application patterns through a set of connection Policies that can be configured by the developer.

## Terminology

- *Client*

Represents the underlying MQTT5 client.

- *Connection Establishment*

The system should be able to establish a connection with the MQTT broker using  [connection settings](connection-settings.md) that will include the TCP address and Port, the TLS requirements and authentication credentials.

The system should support secure MQTT connections using TLS/SSL.

- *Disconnection*

The client should expose an event to inform about disconnection events. Each event includes a `ReasonCode` and an optional `ReasonDescription`. 

There are different cases for connection lost.

1. TCP Connection is alive, and the server terminates the connection (eg, on pod restart). In this case the client is notified immediately.
2. TCP connection is alive, and the server closes the connection (eg. because a protocol error). In this case the client is notified immediately.
3. TCP connection is not available, eg. poor network quality or WiFi reconnects. In this case the client will only be notified when sending the PINGREQ -configurable via KEEPALIVE-, or when trying to publish (PUBACK, PUBREC, PUBREL, or PUBCOMP).

Each case might require different _reconnection strategies_.

- *Reconnects*

In case of connection loss, the system should automatically attempt to reconnect to the MQTT broker. The system should have a configurable delay between reconnection attempts and a maximum number of reconnection attempts. The reconnect will reuse the same `connection-settings` and should be able to refresh the underlying credentials (tokens or certs).

We assume all connections happen as persistent sessions and the subscriptions will be available on the broker per clientId.

- *Retries*

When the connection is dropped, the system will try to reconnect with a configurable policy, eg: Exponential BackOff with Jitter, and a maximum number of retries.

If a message fails to be published, the system should retry publishing the message. The number of retry attempts and the delay between retries should be configurable.

- *Internal Queues*

The system should maintain an internal queue of messages to be published. This queue ensures that messages are not lost if the connection to the MQTT broker is temporarily lost. The system should be able to configure the maximum size of this queue.

- *Message Acknowledgement*

The system should handle PUBACK, PUBREC, PUBREL, and PUBCOMP packets to ensure QoS 1 message delivery.

- *Keep Alive*

The system should send PINGREQ packets to the broker to keep the connection alive when there is no data flow.

- *Last Will and Testament*

The system should be able to set up a Last Will and Testament message that will be sent by the broker if the client disconnects ungracefully.

- *Restarts*

When there is no connection management in place, any connection issue might result in a exception, that if is not handled by the application might crash the process, provoking a restart.

Depend on the application type to define which exceptions to observe and how to handle the application restarts.

## Scenarios

There are different types of applications with different networking constraints:

| Application Type | Network Type | Constraints  | Comments |
|---------------  | ------------| -------- | -------- |
|K8s Pod Single Node| K8s Network| Stable network| There is still the case where the broker is restated (eg after an update) and the MQTT connection needs to be restores. We need to validate persistent sessions are restored after the broker restart|
|K8s Pod Multiple Node| K8s Network| Node restarts/update | In a multinode cluster, the pods need to be resilient to node moves, however this operation always results in pod restart|
| MPU headless| LAN/WAN| Assume network connectivity issues| When the client connect to a broker through a WAN/LAN the application needs to react to network changes using policies to describe the Reconnects and PUB behavior where the connection is not available. Application restarts might be acceptable |
| MPU GUI| LAN/WAN| Assume network connectivity issues| GUI applications must react to connectivity changes without a restart|
| MCU headless| LAN/WAN| Assume network connectivity issues| These devices usually have memory constraints, and might require a more fine-control tuning during offline operations.|

For Cloud Native applications running in a cluster, the network layer is guaranteed, and it should embrace the probing/readiness flags provided by K8s.

For connections established network without quality guarantees such as WiFi or WAN, the connection management becomes critical to avoid crashing the hosting application.

## Storing/Draining messages

Based on the application type there might be different requirements per each application type, so the SDK needs to expose an API to enable the app developer to configure the right policy to store messages such as:

- How many bytes can we store when offline?
- What happens when the limit is reached?
- LIFO/FIFO policies to drain stored messages

## Client Connection and Binders

- Connection is owned and managed by the application
- The application create Envoy/Binders injecting the connection
- The connection can be managed outside of the Binders
- The lifetime of the connection instance is decoupled from the lifetime of the binder instance
