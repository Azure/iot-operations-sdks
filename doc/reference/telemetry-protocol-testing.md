# Testing for Telemetry Protocol

Telemetry Protocol is based on well-defined sequences of PUB and PUBACK packets to implement a request/response pattern.

Telemetry protocol must be tested using normative unit tests that assert the _expected_ behavior of the protocol. This MUST include at the very least:

* Correct MQTT semantic for PUB and PUBACK ordering: PUBs have to be re-transmitted and ACKed in the same order they have been sent and observed by the participating clients. See [section 4.6 Message Ordering](https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901240) in MQTT5 specification.
* Correct de-duplication of requests.

Unit tests must account for at least the following occurrences:

* PUB packet loss.
* PUBACK packet loss.
* Duplicate requests and/or responses.
* Disconnection and re-connection (both for sender and receiver), both due to client failure and/or client being disconnected by the broker (e.g., on account of backpressure).
* Failure and sub-sequent re-connection, both on sender and receiver side.

Unit tests MUST be exhaustive, and therefore MUST neither assume correct behavior of MQTT client nor assume correct behavior of protocol stack in SDK and broker.

> [!CAUTION]
> List may not be exhaustive, update as needed.

## Normative Definition of Tests

### Principles and Assumptions

1. Normative definition of tests assumes MQTT clients on sender and receiver sides are already connected and subscribed to response topics.
1. Normative definition of tests only depends on sender and receiver,and does not distinguish between MQTT client and SDK boundaries.
1. Normative definion of tests aspires to be abstract and not assume any language specific constructs.
1. Protocol errors should be as fine grain and descriptive as possible.
1. Protocol errors should use error codes for brevity. Error messages as strings should be adopted for usability, but should never be transmitted on the wire.
1. Protocol errors should be surfaced to application using language-appropriate constructs.
1. Normative definition of tests assumes protocol uses MQTT5 user properties to convey errors **<span style="color:red"><TBD: is this correct? Correct test definitions if not>**.
1. Normative definition of tests may also cover cases that are practically impossible because of actual implementation. This should be considered a negative, non-relevant case and may not be implemented.

### Sender Expected Behavior

#### Sender happy path

| Number | Normative Statement | Expected |
| --| - | - |
1 | Sender sends one legal message. | One message is validated. |
2 | Sender sends 2 or more legal messages. | Sender receives one distinct validation for each message. 
3 | Sender sends one legal message with metadata. | One message is validated and the sent metadata matches expectations. |

#### Single telemetry

| Number | Normative Statement | Expected behavior |
| - | - | - |
1 | Sender with empty topic pattern sends a message. | Sender throws `ArgumentException`. |
2 | Sender linked to MQTT Client that is not V5. | Sender throws `PlatformNotSupportedException`. |
3 | Sender sends telemetry with malformed payload. | Sender throws `SerializationException`. |
4 | Sender assigned invalid Topic Namespace. | Sender throws `ArgumentException` |

#### Sequence of telemetry data

| Number | Normative Statement | Expected behavior |
| - | - | - |
1 | Sender sends N same messages.| Sender validates once for each message that successfully sends. |
2 | Sender sends N different messages. | Sender validates once for each message that successfully sends. |
3 | Sender sends 2 or more same messages where messages' lifetimes do not overlap with each other and first message times out. | Sender must receive no response and an error `TimeoutException` for first message and cached response for all subsequent messages within freshness window. | 

### Receiver happy path

| Number | Normative Statement | Expected |
| - | - | - |
1 | Receiver receives one legal message. Receiver acknowledges that message. | `OnTelemetryReceived` executes exactly once. The mocked mqtt client sees the expected message acknowledged. |
2 | Receiver receives one legal message with metadata. Receiver acknowledges that message. | `OnTelemetryReceived` executes exactly once and the provided metadata matches expectations. The mocked mqtt client sees the expected message acknowledged. |

### Receiver expected behavior

#### Single telemetry

| Number | Normative Statement | Expected behavior |
| - | - | - |
1 | Receiver initialized with unsupported MQTT version | Receiver must throw `PlatformNotSupportedException` on startup.|
2 |Receiver initialized with unsupported topic namespace | Receiver must throw an `ArgumentException` on startup.|
3 |Receiver initialized with unsupported topic pattern | Receiver must throw an `ArgumentException` on startup.|
4 | Receiver initialized legally. | Receiver starts successfully |
5 | Receiver receives message with malformed payload | Receiver does not crash, and does not execute callback. |  
6 | Receiver receives message with mismatching content type from serializer. | Receiver will not attempt to deserialize application message if content types do not match.|
7 | Receiver receives message with no payload. | Receiver does not crash, and does not execute callback. | 
8 | Receiver receives message and the user-supplied callback throws an exception. | Receiver does not crash and does not acknowledge the message. | 

#### Sequence of telemetry

| Number | Normative Statement | Expected behavior |
| - | - | - |
1 | Receiver receives two or more same messages. | `OnTelemetryReceived` executes once for each message.|
2 | Receiver receives n different messages.	| `OnTelemetryReceived` executes n times. |
3 | Multiple receivers using the same MQTT client underneath receive n different messages. | Each receiver executes `OnTelemetryReceived` in accordance to set QoS. |

#### Receiver disconnects, then reconnects

| Number | Normative Statement | Expected behavior |
| - | - | - |
1 | Receiver disconnects after single message is sent to topic. | Receiver gets two identical messages upon reconnection. |
2 | Receiver disconnects after n messages is sent to topic. | Receiver twice the number of expected messages upon reconnection in order they were sent. |

### Broker expected behavior

| Number | Normative Statement | Expected behavior |
| --- | --- | --- |

### List of sender-side errors

**<span style="color:red"><TBD: decide and document literal and value of error and reconcile with text above>**

| **Error code** | **Error description** |
| --- | --- |
