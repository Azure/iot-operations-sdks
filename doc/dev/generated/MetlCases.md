[//]: # (Auto-generated from file MetlCasesProto.xml -- DO NOT MODIFY)

# Testing for RPC Protocol

RPC Protocol is based on well-defined sequences of PUB and PUBACK packets to implement a request/response pattern.

RPC protocol must be tested using normative unit tests that assert the _expected_ behavior of the protocol. This MUST include at the very least:

* Correct MQTT semantic for PUB and PUBACK ordering: PUBs have to be re-transmitted and ACKed in the same order they have been sent and observed by the participating clients. See [section 4.6 Message Ordering](https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901240) in MQTT5 specification.
* Correct de-duplication of requests.
* Correct execution of [_idempotent_](http://link_to_idempotent) and [_non-idempotent_](http://link_to_non_idempotent) methods.
* Correct lifetime for cached responses.
* Correct handling of orphan requests in presence terminal failures of a client or server using [_message_expiry_](http://link_to_message_expiry) at broker side.

Unit tests must account for at least the following occurrences:

* PUB packet loss.
* PUBACK packet loss.
* Duplicate requests and/or responses.
* Disconnection and re-connection (both for sender and receiver), both due to client failure and/or client being disconnected by the broker (e.g., on account of backpressure).
* Failure and sub-sequent re-connection, both on invoker and executor side.
* Serving requests inside and outside the [_timeout_](http://link_to_timeout) window indicated by request invocation, both for [_idempotent_](http://link_to_idempotent) and [_non-idempotent_](http://link_to_non_idempotent) methods.

Unit tests MUST be exhaustive, and therefore MUST neither assume correct behavior of MQTT client nor assume correct behavior of protocol stack in SDK and broker.
See below for categorized tests.

## CommandExecutor test cases

| Normative statement | Expected behavior |
| --- | --- |
| CommandExecutor receives basic valid request. | CommandExecutor sends response and acknowledges request. |
| CommandExecutor requests synchronize on barrier, with dispatch concurrency insufficient to enable all to proceed. | CommandExecutor blocked when attempting to processes all requests concurrently and times out. |
| CommandExecutor requests synchronize on barrier, with dispatch concurrency sufficient to enable all to proceed. | CommandExecutor processes requests concurrently and returns success. |
| CommandExecutor initialized with empty string as command name. | CommandExecutor throws 'invalid configuration' exception. |
| CommandExecutor initialized with idempotent command that has a positive cache TTL. | CommandExecutor starts successfully. |
| CommandExecutor initialized with idempotent command that has a zero cache TTL. | CommandExecutor starts successfully. |
| CommandExecutor initialized with a topic namespace that is invalid. | CommandExecutor throws 'invalid configuration' exception. |
| CommandExecutor initialized with non-idempotent command that has a positive cache TTL. | CommandExecutor throws 'invalid configuration' exception. |
| CommandExecutor initialized with non-idempotent command that has a zero cache TTL. | CommandExecutor starts successfully. |
| CommandExecutor initialized with no request topic string. | CommandExecutor throws 'invalid configuration' exception. |
| CommandExecutor initialized with null command name. | CommandExecutor throws 'invalid configuration' exception. |
| CommandExecutor receives duplicate idempotent request within command timeout, assuming cache is not under storage pressure. | CommandExecutor does not execute command and responds with value from cache. |
| CommandExecutor receives duplicate non-idempotent request within command timeout. | CommandExecutor does not execute command and responds with value from cache. |
| CommandExecutor receives equivalent executor-agnostic idempotent request from different Invoker ID beyond cacheable TTL. | CommandExecutor executes command and responds with value from execution not from cache. |
| CommandExecutor receives equivalent executor-agnostic idempotent request from different Invoker ID within cacheable TTL. | CommandExecutor executes command and responds with value from execution not from cache. |
| CommandExecutor receives equivalent executor-agnostic non-idempotent request from different Invoker ID. | CommandExecutor executes command and responds with value from execution not from cache. |
| CommandExecutor receives equivalent executor-specific idempotent request from different Invoker ID beyond cacheable TTL. | CommandExecutor executes command and responds with value from execution not from cache. |
| CommandExecutor receives equivalent executor-specific idempotent request from different Invoker ID within cacheable TTL. | CommandExecutor executes command and responds with value from execution not from cache. |
| CommandExecutor receives equivalent executor-specific non-idempotent request from different Invoker ID. | CommandExecutor executes command and responds with value from execution not from cache. |
| CommandExecutor receives equivalent idempotent request beyond cacheable TTL. | CommandExecutor executes command and responds with value from execution not from cache. |
| CommandExecutor receives equivalent idempotent request within cacheable TTL, assuming cache is not under storage pressure. | CommandExecutor does not execute command and responds with value from cache. |
| CommandExecutor receives equivalent non-idempotent request. | CommandExecutor executes command and responds with value from execution not from cache. |
| CommandExecutor receives idempotent request that is duplicate except for different Invoker ID. | CommandExecutor executes command and responds with value from execution not from cache. |
| CommandExecutor receives non-idempotent request that is duplicate except for different Invoker ID. | CommandExecutor executes command and responds with value from execution not from cache. |
| CommandExecutor receives request with unexpected system property in metadata. | CommandExecutor ignores unexpected header and sends response with status OK. |
| CommandExecutor receives a request whose expiry time elapses while the CommandExecutor is disconnected. | Request is not acknowledged after CommandExecutor reconnects. |
| CommandExecutor receives request and stalls execution, causing expiry time to be reached. | CommandExecutor does not complete execution and acknowledges request. |
| CommandExecutor receives request with correlation data that is not a GUID string. | CommandExecutor sends response with status BadRequest. |
| CommandExecutor receives request with invalid __ft header. | CommandExecutor sends response with status BadRequest. |
| CommandExecutor receives request with invalid ResponseTopic metadata. | CommandExecutor discards request and acknowledges. |
| CommandExecutor receives request with invalid __ts header. | CommandExecutor sends response with status BadRequest. |
| CommandExecutor receives request with payload that cannot deserialize. | CommandExecutor does not execute command and sends response with status BadRequest. |
| CommandExecutor receives request with no ContentType metadata. | CommandExecutor sends response with status OK. |
| CommandExecutor receives request with no CorrelationData. | CommandExecutor sends response with status BadRequest. |
| CommandExecutor receives request with no MessageExpiry metadata. | CommandExecutor sends response with status BadRequest. |
| CommandExecutor receives request with no payload. | CommandExecutor sends response with status BadRequest. |
| CommandExecutor receives request with no ResponseTopic metadata. | CommandExecutor discards request and acknowledges. |
| CommandExecutor receives request with no __srcId header. | CommandExecutor sends response with status BadRequest. |
| CommandExecutor receives two requests that synchronize so that they complete in reverse order. | CommandExecutor sends responses in reverse order and acknowledges in receipt order. |
| CommandExecutor request topic contains a '{commandName}' token but commandName is not a valid replacement. | CommandExecutor throws 'invalid configuration' exception. |
| CommandExecutor request topic contains a '{commandName}' token and command name is a valid replacement. | CommandExecutor starts successfully. |
| CommandExecutor request topic contains a '{modelId}' token but model ID is not a valid replacement. | CommandExecutor throws 'invalid configuration' exception. |
| CommandExecutor request topic contains a '{modelId}' token but no model ID is specified. | CommandExecutor starts successfully. |
| CommandExecutor request topic contains a {modelId} token and model ID is a valid replacement. | CommandExecutor starts successfully. |
| CommandExecutor receives request with a protocol version that it cannot parse. | CommandExecutor sends response with status NotSupportedVersion. |
| CommandExecutor receives request with unspecified payload format indicator despite UTF8 content type. | CommandExecutor sends response with status OK. |
| CommandExecutor receives request with a protocol version that it does not support. | CommandExecutor sends response with status NotSupportedVersion. |
| CommandExecutor receives valid request containing metadata. | CommandExecutor sends response and acknowledges request. |
| CommandExecutor receives request with mismatched ContentType metadata. | CommandExecutor sends response with status UnsupportedMediaType. |
| CommandExecutor receives request with different topic than subscribed. | CommandExecutor ignores request, and MQTT client auto-acknowledges. |
| CommandExecutor receives request. | CommandExecutor copies request timout value into response message expiry interval. |
| CommandExecutor receives valid request but ACK dropped when publishing response. | Connection automatically re-established, publication retried, success. |
| CommandExecutor receives valid request but ACK fails when publishing response. | CommandExecutor does not throw exception. |
| During initialization, CommandExecutor subscribes but ACK fails. | CommandExecutor throws 'mqtt error' exception. |
| CommandExecutor receives request that stalls during processing until execution timeout reached. | CommandExecutor responds with RequestTimeout. |
| During finalization, CommandExecutor unsubscribes but ACK fails. | CommandExecutor throws 'mqtt error' exception. |
| CommandExecutor user code raises error indicating problem with request content. | CommandExecutor sends error response. |
| CommandExecutor user code raises error indicating problem with request content. | CommandExecutor sends error response. |
| CommandExecutor user code raises error indicating problem with request execution. | CommandExecutor sends error response. |
| CommandExecutor user code sets metadata header with reserved prefix. | CommandExecutor sends error response. |
| CommandExecutor initialized with a topic namespace that is valid. | CommandExecutor starts successfully. |
| CommandExecutor initialized with an execution timeout of zero. | CommandExecutor throws 'invalid configuration' exception. |

## CommandInvoker test cases

| Normative statement | Expected behavior |
| --- | --- |
| CommandInvoker initialized with empty string as command name. | CommandInvoker throws 'invalid configuration' exception. |
| CommandInvoker with executor-agnostic topic pattern invokes command and receives response. | CommandInvoker completes command and acknowledges response. |
| CommandInvoker with executor-specific topic pattern invokes command and receives response. | CommandInvoker completes command and acknowledges response. |
| CommandInvoker invokes command and receives response. | CommandInvoker publication includes protocol version header with expected version value. |
| CommandInvoker initialized with an invalid request topic string. | CommandInvoker throws 'invalid configuration' exception. |
| CommandInvoker initialized with a response topic prefix that is invalid. | CommandInvoker throws 'invalid configuration' exception. |
| CommandInvoker initialized with a response topic suffix that is invalid. | CommandInvoker throws 'invalid configuration' exception. |
| CommandInvoker initialized with a topic namespace that is invalid. | CommandInvoker throws 'invalid configuration' exception. |
| CommandInvoker invokes command and receives response. | CommandInvoker copies Telemetry timout value into message expiry interval. |
| CommandInvoker initialized with no request topic string. | CommandInvoker throws 'invalid configuration' exception. |
| CommandInvoker invokes command but receives no response message. | Invocation throws 'timeout' exception. |
| CommandInvoker initialized with null command name. | CommandInvoker throws 'invalid configuration' exception. |
| CommandInvoker invokes command but ACK dropped when publishing request. | Connection automatically re-established, publication retried, success. |
| CommandInvoker invokes command but ACK fails when publishing request, then repeats invocation. | Invocation throws 'mqtt error' exception, then reinvocation succeeds. |
| CommandInvoker invokes command but ACK fails when publishing request. | Invocation throws 'mqtt error' exception. |
| CommandInvoker with redundantly executor-specific topic pattern invokes command and receives response. | CommandInvoker completes command and acknowledges response. |
| CommandInvoker request topic contains a '{commandName}' token but commandName is not a valid replacement. | CommandInvoker throws 'invalid configuration' exception. |
| CommandInvoker invokes command with request topic that contains an '{executorId}' token but no replacement is specified. | Invocation throws 'invalid argument' exception. |
| CommandInvoker request topic contains a '{modelId}' token but model ID is not a valid replacement. | CommandInvoker throws 'invalid configuration' exception. |
| CommandInvoker request topic contains a '{modelId}' token but no model ID is specified. | CommandInvoker throws 'invalid argument' exception. |
| CommandInvoker invokes command but response not received, then repeats invocation. | Invocation throws 'timeout' exception, then reinvocation succeeds. |
| CommandInvoker receives response with unexpected system property in metadata. | CommandInvoker ignores unexpected header, completes command, and acknowledges response. |
| CommandInvoker receives response message with status indicating the execution function encountered an exceptional condition. | Invocation throws 'execution error' exception. |
| CommandInvoker receives response message with status indicating the service encountered an unexpected condition. | Invocation throws 'internal logic error' exception. |
| CommandInvoker receives response message with status indicating Bad Request and invalid property name/value. | Invocation throws 'invalid header' exception. |
| CommandInvoker receives response message with status indicating Bad Request and no invalid property name. | Invocation throws 'invalid payload' exception. |
| CommandInvoker receives response message with status indicating an invalid state. | Invocation throws 'invalid state' exception. |
| CommandInvoker receives response message indicating the execution function set an illegal metadata property. | Invocation throws 'execution error' exception. |
| CommandInvoker receives response message with status indicating the request data is not valid at the application level. | Invocation throws 'invocation error' exception. |
| CommandInvoker receives response message with status indicating Bad Request and invalid property name. | Invocation throws 'missing header' exception. |
| CommandInvoker receives response message with status indicating Request Timeout. | Invocation throws 'timeout' exception. |
| CommandInvoker receives response message with status indicating an unknown error condition. | Invocation throws 'unknown error' exception. |
| CommandInvoker receives response message with status indicating Unsupported Media Type. | Invocation throws 'invalid header' exception. |
| CommandInvoker receives response message with status indicating the executor does not support the requested protocol version. | Invocation throws 'request version not supported' exception. |
| CommandInvoker receives response message with invalid status property in header. | Invocation throws 'invalid header' exception. |
| CommandInvoker receives response with payload that cannot deserialize. | Invocation throws 'invalid payload' exception. |
| CommandInvoker receives response with no payload. | Invocation throws 'invalid payload' exception. |
| CommandInvoker receives response message with no status property in header. | Invocation throws 'missing header' exception. |
| CommandInvoker response topic prefix contains a '{commandName}' token but commandName is not a valid replacement. | CommandInvoker throws 'invalid configuration' exception. |
| CommandInvoker initialized with a response topic prefix that contains an '{executorId}' token but no replacement is specified. | CommandInvoker throws 'invalid argument' exception. |
| CommandInvoker response topic prefix contains a '{modelId}' token but model ID is not a valid replacement. | CommandInvoker throws 'invalid configuration' exception. |
| CommandInvoker response topic prefix contains a '{modelId}' token but no model ID is specified. | CommandInvoker throws 'invalid argument' exception. |
| CommandInvoker response topic suffix contains a '{commandName}' token but commandName is not a valid replacement. | CommandInvoker throws 'invalid configuration' exception. |
| CommandInvoker initialized with a response topic suffix that contains an '{executorId}' token but no replacement is specified. | CommandInvoker throws 'invalid argument' exception. |
| CommandInvoker response topic suffix contains a '{modelId}' token but model ID is not a valid replacement. | CommandInvoker throws 'invalid configuration' exception. |
| CommandInvoker response topic suffix contains a '{modelId}' token but no model ID is specified. | CommandInvoker throws 'invalid argument' exception. |
| CommandInvoker with response-topic suffix (instead of prefix) invokes command and receives response. | CommandInvoker completes command and acknowledges response. |
| CommandInvoker receives response message with status code that is not recognized. | Invocation throws 'unknown error' exception. |
| CommandInvoker receives response message with invalid __ts header. | Invocation throws 'invalid header' exception. |
| CommandInvoker receives response message with no content type header. | CommandInvoker completes command and acknowledges response. |
| CommandInvoker receives response message with no message expiry header. | CommandInvoker completes command and acknowledges response. |
| CommandInvoker receives response message with a malformed protocol version. | Invocation throws 'response version not supported' exception. |
| CommandInvoker receives response message with an unsupported protocol version. | Invocation throws 'response version not supported' exception. |
| CommandInvoker receives response message with content type other than expected. | Invocation throws 'invalid header' exception. |
| CommandInvoker invokes and completes Command, then invokes the same Command after the timeout period of the first Command instance. | Both commands complete successfully. |
| CommandInvoker invokes and completes Command, then invokes the same Command within the timeout period of the first Command instance. | Both commands complete successfully. |
| CommandInvoker initialized but ACK fails when subscribing. | CommandInvoker throws 'mqtt error' exception. |
| CommandInvoker invokes command twice in succession and receives responses. | CommandInvoker completes commands and acknowledges responses. |
| CommandInvoker invokes command and receives response. | CommandInvoker publication includes source ID header with value of client ID. |
| CommandInvoker with custom response topic invokes command and receives response. | CommandInvoker completes command and acknowledges response. |
| CommandInvoker with custom topic-token map invokes command and receives response. | CommandInvoker completes command and acknowledges response. |
| CommandInvoker invokes command with metadata key that uses reserved prefix. | Invocation throws 'invalid argument' exception. |
| CommandInvoker invokes command with metadata and receives response. | CommandInvoker publication includes metadata. |
| CommandInvoker invokes command with command timeout of duration below one millisecond. | Invocation throws 'invalid configuration' exception. |
| CommandInvoker with topic namespace invokes command and receives response. | CommandInvoker completes command and acknowledges response. |
| CommandInvoker invokes command with command timeout of zero duration. | Invocation throws 'invalid configuration' exception. |

