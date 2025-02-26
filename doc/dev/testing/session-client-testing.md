# Testing for Mqtt Session Client

## Testing Strategy

Unit tests for the MQTT session client should follow these principles:

* Use a mocked MQTT client as the base client that the session client uses to connect/publish/subscribe/etc.
    * This mocked MQTT client must be able to simulate events such as connection loss, PUBACK received, Publish received, etc.
    * Note that this mocked MQTT client is a mock of the underlying MQTT library, not a mock of the IPubSubClient detailed in [Connection Management](/doc/reference/connection-management.md).
* Do not establish any actual network connections 
    * Random network instability could interfere with deliberate simulated network instability or with deliberate simulated network stability.

Integration tests for the MQTT session client should follow these principles:

* The MQTT session client will connect to a valid MQTT broker.
    * The test suite should allow for the integration tests to run against additional brokers as we need to support them.
* Integration test should **not** attempt to simulate a real network interruption nor should it attempt to simulate the broker behaving in a way that breaks the MQTT spec.
    * Only unit tests are allowed to mock the underlying MQTT client and/or fake disconnections.
    * Some integration tests should involve an **actual** network interruption via the MQTT Broker fault injection feature, though. For instance, there should be a test that the session client can publish messages after telling the MQTT Broker to drop the TCP connection.
* No additional "application level" retry logic should exist in these tests.
    * Any actual/accidental network outage should be handled by the MQTT session client even if the test isn't about that outage.

Longhaul tests for the MQTT session client should follow these principles:

* The MQTT session client will connect to to a valid MQTT broker.
    * The test suite should allow for the integration tests to run against additional brokers as we need to support them.
* No additional "application level" retry logic should exist in these tests.
    * Any actual/accidental network outage should be handled by the MQTT session client even if the test isn't about that outage.

Stress tests for the MQTT session client should follow these principles:

* The MQTT session client will connect to to a valid MQTT broker.
    * The test suite should allow for the integration tests to run against additional brokers as we need to support them.
* No additional "application level" retry logic should exist in these tests.
    * Any actual/accidental network outage should be handled by the MQTT session client even if the test isn't about that outage.
* The MQTT Broker has some fault injection capabilities and these should be used to aggressively stress the session client's ability to maintain a connection and session.

## Normative Definition of Unit Tests

### MQTT Session Client happy paths

| Number | Normative Statement | Expected behavior |
| - | - | - |
| 1 | MQTT Session Client starts/connects | The mock MQTT client should have its `connect` API invoked. A mocked CONNACK should be returned from this invocation and that same CONNACK should be returned by the MQTT session client. This CONNACK should have the clean `isSessionPresent` set to false |
| 2 | MQTT Session Client publishes a message | The mock MQTT client should have its `publish` API invoked. That `publish` API should have the same payload, user properties, topic, etc that the session client's message had. A mocked PUBACK should be returned from this invocation and that same PUBACK should be returned by the MQTT session client.|
| 3 | MQTT Session Client subscribes to a topic | The mock MQTT client should have its `subscribe` API invoked. That `subscribe` API should have the same  topic, user properties, etc that the session client provided. A mocked SUBACK should be returned from this invocation and that same SUBACK should be returned by the MQTT session client.|
| 4 | MQTT Session Client unsubscribes from a topic | The mock MQTT client should have its `unsubscribe` API invoked. That `unsubscribe` API should have the same  topic that the session client provided. A mocked UNSUBACK should be returned from this invocation and that same UNSUBACK should be returned by the MQTT session client.|
| 5 | MQTT Session Client receives a message | The mock MQTT client should simulate receiving a message (for example, invoking the callback for "on message received"). The MQTT session client's message callback should then receive a message with the same payload, user properties, topic, etc. The session client should then acknowledge the message and the mock MQTT client should have its `acknowledgePublish` function invoked.|
| 6 | MQTT Session Client enqueues a Publish | The mock MQTT client should have its `publish` API invoked. That `publish` API should have the same payload, user properties, topic, etc that the session client's message had. The session client should invoke the relevant callback to notify the application layer that this message was published. |
| 7 | MQTT Session Client enqueues a Subscribe | The mock MQTT client should have its `subscribe` API invoked. That `subscribe` API should have the same topic, user properties, etc that the session client's message had. The session client should invoke the relevant callback to notify the application layer that this subscribe was sent and acknowledged. |
| 8 | MQTT Session Client enqueues an Unsubscribe | The mock MQTT client should have its `unsubscribe` API invoked. That `unsubscribe` API should have the same topic, user properties, etc that the session client's message had. The session client should invoke the relevant callback to notify the application layer that this unsubscribe was sent and acknowledged. |

### MQTT Session Client PUBACK ordering and queueing tests

Since the session client needs to provide PUBACK ordering guarantees, we'll need some unit tests that cover the expected scenarios.

| Number | Normative Statement | Expected behavior |
| - | - | - |
| 1 | MQTT Session Client receives two messages and the client ACKs them in order. | The mock MQTT client should have its `acknowledge` API invoked for both messages in the same order as they were received. |
| 2 | MQTT Session Client receives two messages and the client ACKs them in reverse order. | The mock MQTT client should have its `acknowledge` API invoked for both messages in the same order as they were received. |
| 3 | MQTT Session Client receives two messages and the client only ACKs the second message. | The mock MQTT client should **not** have its `acknowledge` API invoked for either of the received messages. |
| 4 | MQTT Session Client receives one message and the client ACKs message after a disconnect occurs. | The mock MQTT client should **not** have its `acknowledge` API invoked for the received messages. |
| 5 | MQTT Session Client receives two messages, then the client only ACKs the second message, then a disconnect occurs. | The mock MQTT client should **not** have its `acknowledge` API invoked for either of the received messages. |


### MQTT Session Client non-network error simple retry paths

For the following tests, each scenario involves testing how the session client responds to encountering an unsuccessful but retriable error code. Each scenario should be a parameterized test where each of the unsuccessful but retriable error codes is the argument. For instance, scenario 1 should be run once with CONNACK code 0x89 (server busy), once for 0x97 (quota exceeded), and once for any other CONNACK code defined in [this section](#retriable-connack-codes).

Note that this section does **not** cover network level errors.

| Number | Normative Statement | Expected behavior |
| - | - | - |
| 1 | MQTT Session Client starts/connects | The mock MQTT client should have its `connect` API invoked. A mocked CONNACK with an [unsuccessful but retriable reason code](#retriable-connack-codes) should be returned from this invocation. The `connect` API on the mock MQTT client should be automatically invoked again by the session client and a successful CONNACK will be returned this time. This second CONNACK should have the `isSessionPresent` flag set to true. This second mock CONNACK should be returned by the session client's original `connect` invocation. |

#### Retriable CONNACK Codes

The following CONNACK codes should spur the session client to re-send the CONNECT packet.

| Value | Hex | Reason Code Name |
| --- | --- | --- |
| 135 | 0x87 | Not authorized |
| 136 | 0x88 | Server unavailable |
| 137 | 0x89 | Server busy |
| 151 | 0x97 | Quota exceeded |
| 159 | 0x9F | Connection rate exceeded |

#### Retriable PUBACK Codes

All PUBACK codes should be considered unretriable.

#### Retriable SUBACK Codes

All SUBACK codes should be considered unretriable.

#### Retriable UNSUBACK Codes

All UNSUBACK codes should be considered unretriable.

### MQTT Session Client bad configuration paths

If the session client tries to connect/publish/subscribe/unsubscribe with a badly formed or incorrectly configured request (Payload format invalid, topic name invalid, unsupported protocol, etc) the general expectation is for that error to be returned/thrown by the session client without retrying the operation.

| Number | Normative Statement | Expected behavior |
| - | - | - |
| 1 | MQTT Session Client starts/connects | The mock MQTT client should have its `connect` API invoked. A mocked CONNACK with an [unsuccessful and unretriable reason code](#unretriable-connack-codes) should be returned from this invocation. The `connect` API on the mock MQTT client should not be invoked again and the initial CONNACK should be returned by the session client. |
| 2 | MQTT Session Client publishes a message | The mock MQTT client should have its `publish` API invoked. That `publish` API should return a mocked PUBACK with an [unsuccessful and unretriable PUBACK code](#unretriable-puback-codes). The `publish` API on the mock MQTT client should not be invoked again and the initial PUBACK should be returned by the session client's original `publish` invocation.|
| 3 | MQTT Session Client subscribes to a topic | The mock MQTT client should have its `subscribe` API invoked. That `subscribe` API should return a mocked SUBACK with an [unsuccessful and unretriable SUBACK code](#unretriable-suback-codes). The `subscribe` API on the mock MQTT client should not be invoked again and the initial SUBACK should be returned by the session client's original `subscribe` invocation.|
| 4 | MQTT Session Client unsubscribes from a topic | The mock MQTT client should have its `unsubscribe` API invoked. That `unsubscribe` API should return a mocked UNSUBACK with an [unsuccessful and unretriable UNSUBACK code](#unretriable-suback-codes). The `unsubscribe` API on the mock MQTT client should not be invoked again and the initial UNSUBACK should be returned by the session client's original `unsubscribe` invocation.|

#### Unretriable CONNACK Codes

The following CONNACK codes should **not** spur the session client to re-send the CONNECT packet.

| Value | Hex | Reason Code Name |
| - | - | - |
| 128 | 0x80 | Unspecified error |
| 129 | 0x81 | Malformed packet |
| 130 | 0x82 | Protocol error |
| 131 | 0x83 | Implementation specific error |
| 132 | 0x84 | Unsupported Protocol Version |
| 133 | 0x85 | Client Identifier not valid |
| 134 | 0x86 | Bad user name or password |
| 138 | 0x8A | Banned |
| 140 | 0x8C | Bad authentication method |
| 144 | 0x90 | Topic Name invalid |
| 149 | 0x95 | Packet too large |
| 153 | 0x99 | Payload format invalid |
| 154 | 0x9A | Retain not supported |
| 155 | 0x9B | QoS not supported |
| 156 | 0x9C | Use another server |
| 157 | 0x9D | Server moved |

#### Unretriable PUBACK Codes

All PUBACK codes should be considered unretriable.

#### Unretriable SUBACK Codes

All SUBACK codes should be considered unretriable.

#### Unretriable UNSUBACK Codes

All UNSUBACK codes should be considered unretriable.

### MQTT Session Client network error simple retry paths

For the following tests, each scenario involves testing how the session client responds to encountering a network drop mid-operation. Similar to testing the unsuccessful CONNACK/PUBACK/SUBACK/UNSUBACK codes, each scenario should be a parameterized test where the argument is the type of exception thrown by the MQTT client as detailed in [Retriable network issues](#retriable-network-issues).

Note that this section does **not** cover receiving CONNACK/PUBACK/SUBACK/UNSUBACK packets that contain an unsuccessful MQTT-level code.

| Number | Normative Statement | Expected behavior |
| - | - | - |
| 1 | MQTT Session Client starts/connects | The mock MQTT client should have its `connect` API invoked. The mock client should simulate a network loss upon this first invocation and should throw an exception that best matches the MQTT library's actual thrown exception in this scenario. The `connect` API on the mock MQTT client should be automatically invoked again by the session client and a successful CONNACK will be returned this time. This second mock CONNACK should be returned by the session client's original `connect` invocation. |
| 2 | MQTT Session Client publishes a message | The mock MQTT client should have its `publish` API invoked. The mock client should simulate a network loss upon this first invocation and should throw an exception that best matches the MQTT library's actual thrown exception in this scenario. The mock client's `connect` API should be invoked again and should return a successful CONNACK. The `publish` API on the mock MQTT client should be automatically invoked again by the session client and a successful PUBACK will be returned this time. This second mock PUBACK should be returned by the session client's original `publish` invocation.|
| 3 | MQTT Session Client subscribes to a topic | The mock MQTT client should have its `subscribe` API invoked. The mock client should simulate a network loss upon this first invocation and should throw an exception that best matches the MQTT library's actual thrown exception in this scenario. The mock client's `connect` API should be invoked again and should return a successful CONNACK. The `subscribe` API on the mock MQTT client should be automatically invoked again by the session client and a successful SUBACK will be returned this time. This second mock SUBACK should be returned by the session client's original `subscribe` invocation.|
| 4 | MQTT Session Client unsubscribes from a topic | The mock MQTT client should have its `unsubscribe` API invoked. The mock client should simulate a network loss upon this first invocation and should throw an exception that best matches the MQTT library's actual thrown exception in this scenario. The mock client's `connect` API should be invoked again and should return a successful CONNACK. The `unsubscribe` API on the mock MQTT client should be automatically invoked again by the session client and a successful UNSUBACK will be returned this time. This second mock UNSUBACK should be returned by the session client's original `unsubscribe` invocation.|
| 5 | MQTT Session Client automatically handles a disconnect. | The mock MQTT client should start in a connected state. Then the mock client should simulate a network loss and should throw an exception that best matches the MQTT library's actual thrown exception in this scenario. The `connect` API on the mock MQTT client should be automatically invoked by the session client with the `CleanStart` flag set to false and a successful CONNACK will be returned with the `isSessionPresent` flag set to true. |
| 6 | MQTT Session Client automatically handles a disconnect but can't recover the session. | The mock MQTT client should start in a connected state. Then the mock client should simulate a network loss and should throw an exception that best matches the MQTT library's actual thrown exception in this scenario. The `connect` API on the mock MQTT client should be automatically invoked by the session client with the `CleanStart` flag set to false and a successful CONNACK will be returned with the `isSessionPresent` flag set to false. The session client should then notify the application layer that the session was lost. |
| 7 | MQTT Session Client enqueues a publish while disconnected. | The mock MQTT client should start in a connected state. The mock client should simulate a disconnect and reject all reconnection attempts. Then the session client should enqueue one publish. Once that publish is enqueued, the mock client should allow the next reconnection attempt to succeed. The publish should go through and the session client should report the successful delivery of the publish via callback. |
| 8 | MQTT Session Client enqueues a subscribe while disconnected. | The mock MQTT client should start in a connected state. The mock client should simulate a disconnect and reject all reconnection attempts. Then the session client should enqueue one subscribe. Once that subscribe is enqueued, the mock client should allow the next reconnection attempt to succeed. The subscribe should go through and the session client should report the successful delivery of the subscribe via callback. |
| 9 | MQTT Session Client enqueues an unsubscribe while disconnected. | The mock MQTT client should start in a connected state. The mock client should simulate a disconnect and reject all reconnection attempts. Then the session client should enqueue one unsubscribe. Once that unsubscribe is enqueued, the mock client should allow the next reconnection attempt to succeed. The unsubscribe should go through and the session client should report the successful delivery of the unsubscribe via callback. |
| 10 | MQTT Session Client enqueues a publish, a subscribe and an unsubscribe while disconnected. | The mock MQTT client should start in a connected state. The mock client should simulate a disconnect and reject all reconnection attempts. Then the session client should enqueue one publish, one subscribe, and one unsubscribe. Once they are all enqueued, the mock client should allow the next reconnection attempt to succeed. The publish, subscribe, and unsubscribe should go through and the session client should report the successful delivery of the subscribe via callback.|

#### Retriable network issues

Unlike with the earlier section on retriable MQTT-level error codes, there is no well-defined list of network errors that covers the expected behavior of all MQTT clients regardless of language. At best, this document can roughly categorize some common exception types to look for your specific language's MQTT library to throw. Manual testing may be necessary to discover the different exceptions that your library may throw.

| Retriable Exception Type | Description |
| - | - |
| NoSuchHostException | A connection could not be established to the MQTT broker |
| SocketException | A problem was encountered with the socket used to establish the connection |
| ConnectionTimeoutException | A connection could not be established because the MQTT broker did not respond in time |
| KeepAliveException | The MQTT broker did not respond in time to a keep-alive ping so the connection was presumed dead |

### MQTT Session Client Retry Policy Tests

| Number | Normative Statement | Expected behavior |
| - | - | - |
| 1 | MQTT Session Client reconnects and the retry policy is consulted. | The session client should start in a connected state. The session client should have a custom retry policy in place with logic to detect if it was invoked or not. The mock client should simulate a disconnect and should reject the first X number of reconnect attempts but then allow the next reconnect to succeed. Verify that the retry policy was consulted X+1 times during this process. |
| 2 | MQTT Session Client reconnects and the retry policy halts reconnection. | The session client should start in a connected state. The session client should have a custom retry policy in place with logic to never retry anything. The mock client should simulate a disconnect and should accept any reconnect attempt. The session client should report via callback that the session was lost. The test should also verify that the retry policy callback was invoked exactly once. |


### MQTT Session Client Ordering Tests

| Number | Normative Statement | Expected behavior |
| --- | --- | --- |
| 1 | MQTT Session Client sends publishes in order on a stable connection | The session client should enqueue two publish operations. After enqueueing these publishes, the mock MQTT client should receive, in order, the two publishes and should return a successful PUBACK. The PUBACKs should be returned by the original respective publish operations. |
| 2 | MQTT Session Client sends publishes in order after reconnect | The session client should be in a simulated disconnected state when it enqueues two publish operations. After enqueueing these publishes, simulate a successful reconnection. The mock MQTT client should receive, in order, the two publishes from before the reconnect and should return a successful PUBACK. The PUBACKs should be returned by the original respective publish operations. |
| 3 | MQTT Session Client sends PUBACKs in order of when each message was received | The mock MQTT client should simulate receiving two PUBLISH packets. The application layer should deliberately only acknowledge the second received message. The mock MQTT layer should not see any invocations that acknowledge a PUBLISH within a reasonable timeframe. The application layer should then acknowledge the first received message. The mock MQTT layer should then see both messages get acknowledged in the order the messages were originally received. |

### Miscellaneous Tests

| Number | Normative Statement | Expected behavior |
| - | - | - |
| 1 | MQTT Session Client clears pending PUBACKs on disconnect | The mock MQTT client should simulate a PUBLISH being received. While the session client propagates that message up to the application layer, simulate a network disconnect. The mock MQTT layer should not see any `acknowledgePublish` invocations. |
| 2 | MQTT Session Client should notify user if session was lost | The session client should be in a simulated connected state. Then a simulated disconnect should occur. The mock MQTT client should see its `connect` API invoked and should return a CONNACK packet with the `sessionPresent` flag set to false. The session client should then execute the callback to notify the application layer that the session was lost. |

## Normative Definition of Integration Tests
 
### MQTT Session Client Connection Tests

The session client must be able to connect to MQTT brokers with the same variety of authentication and authorization schemes that the underlying MQTT client can. This includes TLS-enabled connections, client certificates, username + password, etc. For the following tests, the MQTT broker must be configured to reject the connection if the client's authentication and/or authorization don't match expectations. 

| Number | Normative Statement | Expected behavior |
| - | - | - |
| 1 | MQTT Session Client connects to an MQTT broker with TLS authentication | The broker should accept the connection if the TLS authentication is valid. |
| 2 | MQTT Session Client connects to an MQTT broker with client certificates for authorization | The broker should accept the connection if the authorization is valid. |
| 3 | MQTT Session Client connects to an MQTT broker with username + password for authorization | The broker should accept the connection if the authorization is valid. |
| 4 | MQTT Session Client connects to an MQTT broker with client certificates for authorization and TLS authentication | The broker should accept the connection if the authorization and authentication are both valid. |
| 5 | MQTT Session Client connects to an MQTT broker with username + password for authorization and TLS authentication | The broker should accept the connection if the authorization and authentication are both valid. |

### MQTT Session Client Basic Operation Tests

This section of tests covers the basic pub/sub/unsub tests that assume a stable connection. Note that connection losses are possible when running these tests since there is an actual connection. The session client itself should seamlessly handle these disconnections, though. 

| Number | Normative Statement | Expected behavior |
| - | - | - |
| 1 | MQTT Session Client publishes a message. | The session client should connect to the broker and publish a single message. The session client should return a successful PUBACK. |
| 2 | MQTT Session Client subscribes to a topic. | The session client should connect to the broker and subscribe to a topic on the broker. The session client should return a successful SUBACK. The client should then publish a message to that same topic. Then the session client should receive this published message from the broker. |
| 3 | MQTT Session Client unsubscribes from a topic. | The session client should connect to the broker and subscribe to a topic on the broker. The session client should return a successful SUBACK. The client should then unsubscribe from that same topic and receive a successful UNSUBACK. Finally, the session client should try to publish a message to that same topic and see the publish acknowledged with "NoMatchingSubscribers" to validate that the unsubscription was successful. |
| 4 | MQTT Session Client enqueues a publish. | The session client should connect to the broker and enqueue a single publish. The session client should eventually invoke the relevant callback declaring that the message was sent successfully. |
| 5 | MQTT Session Client enqueues a subscribe. | The session client should connect to the broker and enqueue a single subscribe. The session client should eventually invoke the relevant callback declaring that the subscribe was sent successfully. |
| 6 | MQTT Session Client enqueues an unsubscribe. | The session client should connect to the broker and enqueue a single unsubscribe. The session client should eventually invoke the relevant callback declaring that the unsubscribe was sent successfully. |

### Fault injection tests

While the MQTT Broker fault injection capabilities are limited at the moment, it is possible for us to develop an MQTT broker that is capable of responding to certain requests by dropping a connection, returning an unsuccessful acknowledgement with a specified reason code and more. With a broker like that it is possible to have integration tests like these:

| Number | Normative Statement | Expected behavior |
| - | - | - |
| 1 | MQTT Session Client sends a CONNECT, the broker rejects it. | The session client should try to connect, but the broker should reject the connection with a [retriable CONNACK reason code](#retriable-connack-codes). The client should automatically try to connect again and the broker should accept the connection this time. The session client should not report any errors. |
| 2 | MQTT Session Client publishes a message, the broker disconnects the client instead of acknowledging the message. | The session client should publish a message, but the broker should disconnect the client instead of acknowledging the publish. Then the client should reconnect and then successfully publish the message. |
| 3 | MQTT Session Client subscribes, the broker disconnects the client instead of acknowledging the subscribe. | The session client should send a subscribe request, but the broker should disconnect the client instead of acknowledging the subscribe. Then the client should reconnect and then successfully send the subscribe request. |
| 4 | MQTT Session Client unsubscribes, the broker disconnects the client instead of acknowledging the subscribe. | The session client should send an unsubscribe request, but the broker should disconnect the client instead of acknowledging the unsubscribe. Then the client should reconnect and then successfully send the unsubscribe request. |
| 5 | MQTT Session Client is idle, the broker disconnects the client. | The session client should be in a connected state, but then the broker should disconnect the client with a [retriable CONNACK reason code](#retriable-connack-codes). The client should automatically try to connect again and the broker should accept the connection this time. The session client should not report any errors. |
