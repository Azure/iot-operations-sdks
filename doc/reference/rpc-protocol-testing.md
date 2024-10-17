# Testing for RPC Protocol

RPC Protocol is based on well-defined sequences of PUB and PUBACK packets to implement a request/response pattern.

RPC protocol must be tested using normative unit tests that assert the _expected_ behavior of the protocol. This MUST include at the very least:
* Correct MQTT semantic for PUB and PUBACK ordering: PUBs have to be retransmitted and ACKed in the same order they have been sent and observed by the participating clients. See [section 4.6 Message Ordering](https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901240) in of MQTT5 specification.
* Correct de-duplication of requests.
* Correct execution of _idempotent_ and _non-idempotent_ methods.
* Correct lifetime for cached responses.
* Correct handling of orphan requests in presence terminal failures of a client or server using _message_expiry_ at broker side.

Unit tests must account for at least the following occurrences:
* PUB packet loss.
* PUBACK packet loss.
* Duplicate requests and/or responses.
* Disconnection and re-connection (both for sender and receiver), both due to client failure and/or client being disconnected by the broker (e.g., on account of backpressure).
* Failure and sub-sequent re-connection, both on invoker and executor side.
* Serving requests inside and outside the _timeout_ window indicated by request invocation, both for _idempotent_ and _non-idempotent_ methods.

Unit tests MUST be exhaustive, and therefore MUST neither assume correct behavior of MQTT client nor assume correct behavior of protocol stack in SDK and broker.

## Normative Definition of Tests

### Principles and Assumptions

1. Normative definition of tests assumes MQTT clients on invoker and executor sides are already connected and subscribed to response topics.
1. Normative definition of tests only depends on invokers and executors,and does not distinguish between MQTT client and SDK boundaries.
1. Normative definion of tests aspires to be abstract and not assume any language specific constructs.
1. Protocol errors should be as fine grain and descriptive as possible.
1. Protocol errors should use error codes for brevity. Error messages as strings should be adopted for usability, but should never be transmitted on the wire.
1. Protocol errors should be surfaced to application using language-appropriate constructs.
1. Normative definition of tests assumes protocol uses MQTT5 user properties to convey errors **<span style="color:red"><TBD: is this correct? Correct test definions if not>**.
1. Normative definition of tests may also cover cases that are practically impossible because of actual implementation. This should be considered a negative, non-relevant case and may not be implemented.
1. Executor may serve requests by indicating a freshness value for the response as a time-to-live.
1. Executor must use a cache for de-duplicating requests within the request timeout window. This is necessarynot to invoke non-idempotent method multiple times, which would result in incorrect app logic, and optimize execution by avoiding executing same request multiple times  unnecessarily.
1. Two requests or two responses are considered identical (_same_ request) when the PUBs associated to each request, or response, have same correlation ID and same payload **<span style="color:red"><TBD: anything else? Do we also need same user properties?>**. Publish ID may differ.

### Invoker happy path

| Number | Normative Statement | Expected behavior |
| - | - | - |
| 1 | Invoker sends legal request | Invoker will receive one legal response. |
| 2 | Invoker sends 2 or more legal requests for an non-idempotent method | Invoker will receive one legal response for each request, where each response is produced after a server method invocation. Invoker will issue all requests without blocking. |
| 3 | Invoker sends N legal requests in a row for an non-idempotent method with method execution set not to exceed M concurrent invocations, where M << N  | Invoker will receive one legal response for each request, where each response is produced after one and only one distinct server method invocation. Invoker will issue all requests without blocking. Executor will execute at most M parallel requests. |
| 4 | Invoker sends N legal requests in a row for an idempotent method with a freshness value T with method execution set not to exceed M concurrent invocations, where M << N, and execution time for the invoked method is t such that N * t << T | Invoker will receive one legal response for each request, where each response is produced from one single server method invocation across all requests (as opposed to two or more server method invocations, or one invocation for each request). Invoker will issue all requests without blocking. Execution of method only happen once. |

### Invoker expected behavior

#### Single request

| Number | Normative Statement | Expected behavior |
| - | - | - |
| 1 | Invoker sends request with malformed payload | If a request payload cannot be decoded by the intended executor, invoker must receive error <span style="color:red">HttpStatusCode.BadRequest</span> in response user properties. |
| 2 | Invoker sends legal (decodable) request for non-existing command | Invoker must receive error <span style="color:red">HttpStatusCode.NotImplemented</span> in response user properties.  |
| 3* | Invoker sends request with wrong content type in header to executor. | Executor must receive error <span style="color:red"> Http.UnsupportedMediaType</span> with the indication of which parameter type was wrong for at least one parameter in response user properties. |
| 4* | Invoker sends request with wrong parameters type in C# (e.g., an integer cast to a string) | Invoker must receive error <span style="color:red">Http.UnprocessableEntity</span> with the indication of which parameter type was wrong for at least one parameter in response user properties. | 
| 5* | Executor sends a response with wrong content header of payload back to invoker | Invoker must receive error <span style="color:red">NotSupportedException</span> with the indication of which parameter type was wrong for at least one parameter in response user properties. |
| 6* | Invoker cannot deserialize response to specified content type | Invoker must receive error <span style="color:red">SerializationException</span> with the indication of which parameter type was wrong for at least one parameter in response user properties. 
| 7 | Invoker client sends one request for one command with a negative timeout | Invoker must receive error <span style="color:red">ArgumentOutOfRangeException</span> and request should not be sent to broker. | 
| 8 | Invoker client sends one request for one command with a non-zero timeout | Invoker must receive either _one_ response or no response and an error <span style="color:red">TimeoutExcpetion</span>. |
| 9 | Invoker client sends one request for one command with a non-zero timeout but request is dropped | Invoker will retry sending request up until timeout elapses and must receive either _one_ response or no response and an error <span style="color:red">TimeoutException</span>. |
| 10 | Invoker client sends one request for one command with a non-zero timeout but PUBACK for request is dropped | Invoker must retry sending request up until timeout elapses and must receive either _one_ response or no response and an error <span style="color:red">TimeoutException</span>. |

#### Sequence of requests

| Number | Normative ** | Expected behavior |
| - | - | - |
| 1 | Invoker client sends _N_ _repeated_ requests for one command inside the timeout window of first request | Invoker must receive one response for each request that does not timeout on executor side. If all requests time out, the invoker must receive no response and a error <span style="color:red">TimeoutException</span> for each request. <br/> _This should be tested through an end-to-end test. <br/>An idempotent same request should return the response from the cache. A non-idempotent same request should also return the response from the cache._ |
| 2 | Invoker client sends _N_ _equivalent_ requests for one command inside the timeout window of first request | Invoker must receive one responses for each request that does not timeout on executor side. If all requests timeout invoker must receive no response and a error <span style="color:red">TimeoutException</span> for each request. <br/> _This should be tested through an end-to-end test. <br/>An idempotent equivalent request should return the response from the cache. A non-idempotent equivalent request should be re-executed._ |
| 3 | Invoker client sends _N different_ request for one command and requests lifetime do not overlap with each other (i.e., all requests start after all previous requests' timeout has elapsed already) | Invoker must receive _n different_ responses. |
| 4 | Invoker client sends _2 or more equivalent_ requests for one idempotent command with cacheable response where requests'  lifetime do not overlap with each other (i.e., all requests start after all previous requests' timeout has elapsed already) and first request times out on executor side | Invoker must receive no response and a error <span style="color:red">TimeoutException</span> for first request and cached response for all subsequent requests within freshness window. <br/> _This should be tested through an end-to-end test._ |

#### Invoker disconnects or times out, the re-connects

| Number | Normative Statement | Expected behavior | 
| - | - | - |
| 1 | Invoker disconnects (or is disconnected) after sending request but before receiving PUBACK for request | Invoker must re-send _same_ request upon reconnecting and must receive _one only_ response. | 
| 2 | Invoker disconnects (or is disconnected) after sending request and receiving PUBACK for request | Invoker must receive _one only_ response upon reconnecting. Invoker must discard response if response is ourside timeout window or original request. |
| 3 | Invoker times out waiting for a response | Invoker must receive one error <span style="color:red">TimeoutException</span> and no response. |
| 4 | Invoker times out waiting for the response and re-sends a new request (not _same_ request) for same method | Invoker will receive one error <span style="color:red">TimeoutException</span> for first request and _one only_ response for the second request. |

### Executor happy path

| Number | Normative Statement | Expected behavior |
| - | - | - |
| 1 | Executor client receives a legal request | Executor will send one legal response. |

### Executor expected behavior

#### Single request

| Number | Normative Statement | Expected behavior |
| - | - | - |
| 1*| Executor initialized with unsupported MQTT version | Executor must throw `PlatformNotSupportedException` on startup.|
| 2*| Executor initialized with unsupported topic pattern | Executor must throw an `ArgumentException` on startup. |
| 3*| Executor initialized to execute a non-idempotent command with cacheable duration set to zero | Executor will start successfully. |
| 4*| Executor initialized to execute a non-idempotent command with cacheable duration set to non-zero (both positive and negative) | Executor will throw an `ArgumentException` on startup. Non-idempotent commands are cached up to their command timeout value. |
| 5*| Executor initialized to execute an idempotent command with cacheable duration set to zero or positive value | Executor will start successfully. |
| 6*| Executor initialized to execute an idempotent command with cacheable duration set to negative value | Executor will throw an `ArgumentException` on startup. |
| 7*| Executor initialized with a command timeout of zero or negative value | Executor will throw an `ArgumentException` on startup. |
| 8 | Executor receives request with malformed payload | Correlation data missing - Executor must send error **<span style="color:red">HttpStatusCode.BadRequest**</span> in response user properties. This is an RPC-level error (identified through user property). <br/> Response topic missing - message should not be processed but it should be acknowledged. <br/> Missing request payload - Executor must send error **<span style="color:red">HttpStatusCode.BadRequest**</span> in response user properties. This is an RPC-level error (identified through user property).|
| 9 | Executor receives request for non-existing command | Executor must discard the incoming request received. |
| 10 | Executor receives request but application processing fails | Executor must send error **<span style="color:red">APPLICATION ERROR CODE**</span> in response user properties. This is an application-level error (identified through user property).
| 11 | Executor receives request with the correct parameters type but wrong value | Content type mismatch - Executor must send error **<span style="color:red">HttpStatusCode.UnsupportedMediaType**</span> in response user properties. This is an RPC-level error (identified through user property). <br/> Response deserialization failure - Executor must send error **<span style="color:red">HttpStatusCode.BadRequest**</span> in response user properties. This is an RPC-level error (identified through user property). |
| 12 | Executor receives one request for one command with a zero timeout | Executor must use the default command timeout value set in executor communication options. |
| 13 | Executor receives one request for one command with a non-zero timeout | Executor must try and reply if request timeout has not elapsed upon receiving request. If executor receives _same_ request more than once, then executor shall try and reply with _same_ response if original request timeout has not elapsed (for a non-idempotent command) or if the cache has not expired (for an idempotent command), and drop request thereafter, at which point invoker will observe a timeout and receive no response. |
| 14 | Executor receives one request for one command with a non-zero timeout but response is dropped | Same as above, as broker must re-send the request that was not ACKed. |
| 15 | Executor receives one request for one command with a non-zero timeout but PUBACK for request is dropped | Same as above, as broker must re-send the request that was not ACKed. |
| 16 | A request with a non-zero timeout is received by protocol stack but expires before it can be sent to executor  | Protocol stack must discard expired message, not invoke executor and send no response. Invoker will observe a timeout and receive no response. |

#### Sequence of requests

| Number | Normative Statement | Expected behavior |
| - | - | - |
| 1 | Executor receives two or more _same_ requests for one command inside the timeout window of first request | Executor must send as many responses as the number of request PUBLISH packets that it receives or no response and an error <span style="color:red">REQUEST_TIMEOUT</span> **<span style="color:red"><TBD: decide and document literal and value of error>** if execution exceeds request timeout. Executor must invoke server method only once if the request is for a non-idempotent method. Executor should invoke server method only once if the request is for an idempotent method and cached response is fresh. |
| 2 | Executor receives _n different_ request for one command and requests lifetime do not overlap with each other (i.e., one request when the previous request's timeour has elapsed already) | Executor must send _n different_ responses. Content of the response may be identical if cached response is fresh and method is idempotent. Content of the response may differ if method is non-idempotent. |
| 3*| Executor receives _n different_ request for one command concurrently | Executor must process as many requests in parallel as defined by the dispatcher concurrency value. The incoming requests should be processed and the response should be PUBLISHed as soon as it is available. However, the PUBACK for the incoming request should be sent in the order that the incoming request was received in, and not as per the order of the processed response available. |
| 4*| Multiple executors using the same MQTT client underneath receive _n different_ request for _n_ commands concurrently | The incoming requests should be processed and the response should be PUBLISHed as soon as it is available. However, the PUBACK for the incoming request should be sent in the order that the incoming request was received in, and not as per the order of the processed response available. | 

#### Executor disconnects, then re-connects

| Number | Normative Statement | Expected behavior |
| - | - | - |
| 1 | Executor disconnects (or is disconnected) before sending a PUBACK for request | Executor must observe _same_ request being re-delivered upon reconnecting and will send _one only_ response. Executor will complete request upon receiving PUBACK for response and then send PUBACK for request. |
| 2 | Executor disconnects (or is disconnected) before sending a response and before having sent a PUBACK for request | Executor must observe _same_ request being re-delivered upon reconnecting and must send _only one_ response. If method is not idempotent and duplicate request is observed inside the timeout of the original request, then executor must execute request only once independently of the response freshness. If method is idempotent, executor may execute request more than once and should do so if response is not fresh (cache has expired) and duplicate request is observed inside the timeout of the original request. Executor will complete request upon receiving PUBACK for response and then send PUBACK for request. |
| 3 | Executor disconnects (or is disconnected) after sending a response and before having sent a PUBACK for request | Executor must observe _same_ request being re-delivered upon reconnecting and must send _same_ response. If method is not idempotent and duplicate request is observed inside the timeout of the original request, then executor must execute request only once independently of the response freshness. If method is idempotent, executor may execute request more than once and should do so if response is not fresh (cache has expired) and duplicate request is observed inside the timeout of the original request. Executor will complete request upon receiving PUBACK for response and then send PUBACK for request. |
| 4 | Executor receives _n different requests_ and gets disconnected before acking them. One or more of the unacked requests expire before the client reconnects | Executor must observe the _same_ request for the unexpired requests. It should ack _each unique_ received request from the current session. Any unacked packets from the previous session should not be ack'ed. <br/> _This should be tested through an end-to-end test._|

### Broker expected behavior

| Number | Normative Statement | Expected behavior |
| - | - | - |
| 1 | Broker receives invoker request with non-zero timeout | Broker must honor request timeout by using [message expiry interval](https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901112) and desist from trying and deliver the request is the timeout elapses and delivery has not been initiated, in which case invoker must observe an error <span style="color:red">REQUEST_TIMEOUT</span> **<span style="color:red"><TBD: decide and document literal and value of error>** and no response. |
| 2 | Broker receives invoker request with non-zero timeout, and then invoker disconnects and never reconnects (orphan request) | Broker must honor request timeout by using [message expiry interval](https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901112) and ACK executor response. Broker must use and enforce message expiry also on invoker side, and expire response delivery attempt when request lifetime elapses. |

### List of invoker-side errors

**<span style="color:red"><TBD: decide and document literal and value of error and reconcile with text above>**

| Error code | Error description |
| - | - |
| BAD_PAYLOAD | Payload of request cannot be decoded. |
| COMMAND_NOT_FOUND | Payload of request points to a command that does not exist. |
| BAD_PARAMETER_TYPE | Payload of requests encodes a command invocation with the wrong parameter type. |
| BAD_PARAMETER_VALUE | Payload of requests encodes a command invocation with the wrong parameter value. |
| ILLEGAL_TIMEOUT | Request timeout value is illegal (e.g., zero). |
| REQUEST_TIMEOUT | Request timed out. |
