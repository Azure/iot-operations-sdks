# Session Client

## Problem statement

Within an Azure IoT Operations ecosystem, there are various components that need to establish and maintain a stable MQTT session with the MQTT broker. Currently, every simple component (such as an RPC client or a DSS client) assumes this MQTT session is being maintained for them. This means that users of these components would need to write application-level logic to ensure that their connection and desired session state is maintained. While some users may prefer owning this logic, some would prefer if our client handled this for them.

While there are some managed clients that already exist in the respective MQTT clients that meet some of our requirements, most of them have problems that would prevent us from taking a dependency on them as they currently are.

## Proposal

1. Create an `MqttSessionClient` that wraps our current languages' respective MQTT clients and manages the connection for the user.

1. This `MqttSessionClient` would allow us to not have to write duplicate connection management code in all our different clients.

1. The primary goal for this session client is to meet our connection and session management needs in the various binders and clients.

## Request queue (Publish, Subscribe, Unsubscribe)

The MQTT session client includes a request queue for Publish, Subscribe, and Unsubscribe requests. In the case of normal operations, the queuing mechanism allows the session client to process requests in order and to complete delayed acknowledgement. In the case of connection interruption, the queuing mechanism allows the managed client to track incomplete requests and resume processing upon successful retry.

Some MQTT clients (such as MQTTNet) do not expose a queue like this, so the MQTT session client will need to implement and own this queue. Some clients (such as the Go client) do expose the underlying queue, so they may use it instead.

For languages where our session client library implements the queue, the queue must have a configurable max size. When a queue is maxed out and a new publish/subscribe/unsubscribe, the oldest/youngest publish/subscribe/unsubscribe should be discarded depending on user configuration.

## Initial Connection

When a user is first connecting their session client, they should be allowed to either connect with CleanStart set to true or false. However, the value of this option will have no bearing on the CleanStart flag value that is used for when reconnecting.

## Reconnection

When a session client has lost the connection and tries to reconnect to the MQTT broker, it must use CleanStart=false to try to recover the session.

If the MQTT broker accepts the connection, but the CONNACK includes isSessionPresent=false, then the session client must close the connection and notify the application layer that the session was lost. This particular scenario is considered a "catastrophic error" wherein the user must be made aware that the session was lost because of potential message loss.

The session client should not attempt to fake recovering the session by sending SUBSCRIBE packets to the broker to recover subscriptions lost when the session was lost.

## Disconnection

When a user disconnects the session client, the session client must ensure that a session expiry interval of 0 is sent along with this DISCONNECT packet. This ensures that the broker expires the session immediately after the disconnection.

## Pub Ack Handling

### Ways to acknowledge

The proposed session client will allow users to manually acknowledge a received publish at any time and from any thread. If the language's MQTT library provides it, the proposed client should also allow users to auto-acknowledge messages. Note that auto-acknowledged messages still need to be enqueued in the correct order just like any other acknowledgement.

Some MQTT client libraries (such as the Go Paho client) already provide this feature. Forlibraries that don't, queueing logic will need to exist in the session client layer.

### Ack Ordering

The proposed session client will deliver publish acknowledgements in the order the publishes were received in. The order that the user acknowledges them is disregarded.

In order to provide this guarantee, the proposed session client may only acknowledge a publish if every publish received before it has already been acknowledged. For example, if the client receives publishes 1 and 2, and the user only acknowledges publish 2, neither publish 1 or 2 will actually be acknowledged on the MQTT connection until the user also acknowledges publish 1.

In cases where the user callback for a message never completes, the session client will never send an acknowledgement for the message and will block subsequent messages from being received.

In cases where the user callback for a message throws, the session client will send the acknowledgement as to not block subsequent messages from being acknowledged.

### Session Cleanup

The session client must clear the queue of ACKs if a disconnection event occurs. Any queued acknowledgements that were not sent prior to the disconnect should be abandoned. The expected behavior from a user of this client should be that un-acknowledged messages are re-delivered.

Note that this design means that, if a client receives message1, then message2, the user acks message2, then a disconnect happens, the session client will not send an acknowledgement for
either message. In this case, the client will receive message1 and message2 again. While it is undesirable for the user to see a message again that they believe they have acked already,
QoS 1 behavior provides cover here.

### Acknowledgement API design

When the MQTT session client notifies the application layer that a message was received, the provided message object should contain both a settable flag for opting out of automatic acknowledgement as well as a function for acknowledging the message.

Importantly, the default behavior of the session client should be to automatically acknowledge a message so that any unhandled messages that go un-acked do not block the PUBACK queue.

Due to this design, the session client should be blocking on these callbacks finishing so that the application layer has the opportunity to set the `AutoAcknowledge` flag before the session client attempts to automatically acknowledge the message.

## Session Management - Connection Settings and Retry Policy

The overall principle of session management is either for the application to own the connection and session by passing in an MQTT client to their binders or for the MQTT session client to create the MQTT client and own the connection and session. There is no case of mixed ownership of the connection. 

In the case where the MQTT client is provided, it is the provider’s responsibility to manage all aspects of connection and session. No session client will be constructed in this case.

In the case where the application wants a managed connection, the MQTT session client simplifies the connection setup and connection maintenance process by handling retriable connection interruptions based on user-configured timeout and retry policy specifications. For fatal connection or session failures, the MQTT session client will notify the user application.

We will use a generic connection settings structure to capture MQTT connection specific parameters. The application can use this settings structure to create the MQTT connection or provide the settings to the proposed managed client as a consistent approach to create a connection.

### What is retried

When a user creates an MQTT session client to handles the session management for them, all connection and session level failures (such as connection loss) will be handled by this MQTT session client. All application level failures (such as a publish packet being acknowledged with a non-success reason code) will simply be returned to the user via the respective publish/subscribe/unsubscribe API.

Connection and Session level failures (retried):

* Connection is lost due to keepalive timeout
    * The MQTT session client is expected to send a new CONNECT packet with cleanSession = false to reestablish the session.
* Subscribe/Unsubscribe request is made, but a connection loss happens before any SUBACK/UNSUBACK is received.
    * The MQTT session client is expected to requeue the subscribes and unsubscribes that weren't acknowledged yet and send them again when the connection is re-established.
* Publish request is made, but a connection loss happens before any PUBACK is received.*
    * The MQTT session client is expected to requeue the publishes that weren't acknowledged yet and send them again when the connection is reestablished.
    * This case may already be covered by the underlying MQTT client. If it is, then the session client does not need to resend the unacknowledged messages. 
* Connection is lost due to the session being taken over
    * The MQTT session client is expected to report this as a fatal error to the user and not attempt to recover the session

Application level failures (not retried):

* Publish packet acknowledged with non-success code (0x00).
* Subscribe packet acknowledged with non-success code (0x00, 0x01 or 0x02 depending on requested QoS).
    * Note that this does mean user applications will need to check that each subscription's QoS granted matches what they requested.

Miscellaneous cases:

* Session client reconnects (with clean session = false as it always should), but the server sends sessionPresent = false in the CONNACK
    * The session client should throw a fatal error up to the user to notify them that the session has ended. Session loss may result in message loss since the previous session may have had enqueued messages on the broker side that won't be sent to the client now that the session is lost.
* Session client gets disconnected by the server with a DISCONNECT packet
    * The session client should NOT try to reconnect if the reason code is 0x8E Session Taken Over.
    * In all other cases, the session client should reconnect if the reason code is transient (e.g., 0x97 Quota Exceeded) and should not reconnect if the reason code is permanant (e.g., 0x9D Server Moved)
