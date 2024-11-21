[//]: # (Auto-generated from file MetlSpecProto.xml -- DO NOT MODIFY)

# METL (MQTT Envoy Testing Language) Specification

This document describes the format of the domain-specific language METL, in which Akri.Mqtt unit tests are written.
The syntax of METL is [YAML](https://yaml.org/).
The vocabulary and usage of METL are partially dependent on the class under test, but every test case begins with some unprocessed descriptive matter, optionally followed by `requires`, followed by up to three regions: `prologue`, `actions`, `epilogue`.

A `prologue` region is always required, but `actions` and `epilogue` are optional.
For example, following is a small but complete test case, which verifies only successful initialization:

```yaml
test-name: CommandExecutorRequestTopicModelIdWithoutReplacement_StartsSuccessfully
description:
  condition: >-
    CommandExecutor request topic contains a '{modelId}' token but no model ID is specified.
  expect: >-
    CommandExecutor starts successfully.
prologue:
  executors:
  - request-topic: "mock/{modelId}/test"
    model-id:
```

A common use for `prologue`-only cases is to test initialization error-checking:

```yaml
test-name: CommandInvokerSubAckFailure_ThrowsException
description:
  condition: >-
    CommandInvoker initialized but ACK fails when subscribing.
  expect: >-
    CommandInvoker throws 'mqtt error' exception.
prologue:
  push-acks:
    subscribe: [ fail ]
  invokers:
  - { }
  catch:
    error-kind: mqtt error
    in-application: !!bool false
    is-shallow: !!bool false
    is-remote: !!bool false 
```

Cases that test protocol conformance will generally include at least an `actions` region and often also an `epilogue` region:

```yaml
test-name: TelemetryReceiverReceivesNoPayload_NotRelayed
description:
  condition: >-
    TelemetryReceiver receives telemetry with no payload when one was expected.
  expect: >-
    TelemetryReceiver does not relay telemetry to user code.
prologue:
  receivers:
  - { }
actions:
- action: receive telemetry
  payload:
  packet-index: 0
- action: await acknowledgement
  packet-index: 0
epilogue:
  acknowledgement-count: 1
  telemetry-count: 0
```

### Key/value kinds

There are three kinds of key/value pairs in test cases, with notably different semantics.
*Drive* keys specify values that are used to drive inputs to the test: configuration settings, class properties, method arguments, and message fields.
*Check* keys specify expectations for values that are produced by the class under test; these are roughly analogous to asserts in conventional imperative test code.
*Match* keys indicate abstract correspondences that will receive concrete substitute values when the test executes.
An example of a match key is `correlation-index`; each distinct value will be mapped by the test framework to a distinct correlation ID in a message.
The meaning of null values and omitted keys depends on the kind of key:

* **Drive**
  * Omitted key &mdash; Use the default value to drive the test.
  * Non-null value &mdash; Use the indicated value to drive the test.
  * Null value &mdash; Use a missing or null value when driving the test.

* **Check**
  * Omitted key &mdash; The value is irrelevant or not expected for the test case.
  * Non-null value &mdash; Check that the class under test produces the indicated value.
  * Null value &mdash; Check that the class under test produces the null value or no value.

* **Match**
  * Omitted key &mdash; The value is irrelevant for the test case; no matching will be done.
  * Non-null value &mdash; Match on the indicated value.
  * Null value &mdash; Match on an absent property (e.g., a message with no correlation ID); permitted only when test should generate exactly one possible match, ensuring unambiguous reference.

### Quoted strings

Single and double quotation marks have [slightly different meanings in YAML](https://www.yaml.info/learn/quote.html) but are often interchangeable by making appropriate substitutions of embedded escape sequences.
For test cases written in METL, the following convention is observed:

* Double quotes surround strings that should be used in the test with absolutely literality.
* Single quotes surround strings that may be mapped to programming-language-appropriate values by different test engines.

Unquoted (bare) strings are used for keywords or key phrases in METL, such as the value of an `action` key.
For example:

```yaml
actions:
- action: invoke command
  invocation-index: 0
  metadata:
    "__hasReservePrefix": "userValue"
- action: await invocation
  invocation-index: 0
  catch:
    error-kind: invalid argument
    in-application: !!bool false
    is-shallow: !!bool true
    is-remote: !!bool false
    supplemental:
      property-name: 'metadata'
```

In the above test case, the value of `metadata` is double quoted, indicating that the metadata key must be used verbatim in the test.
By contrast, the value of `property-name` is single quoted, indicating that it may incur non-semantic changes across programming languages.
Since language conventions dictate different casing of property and argument names, the value in the test case is lowercase and contains no separators.
Test engines should de-capitalize and de-separate property names that are camelCase, PascalCase, or snake_case before comparing them to the indicated value.

### Test platform requirements

The value of `requires` is an enumeration of features that the test platform must support for the test case to be run, as in the following example:

```yaml
requires:
- dispatch
- explicit-default
```

If a test platform does not support all of the features enumerated by `requires`, the test case will not be run against the platform.
The platform features are identified via the following enumeration.

#### FeatureKind

The feature kind is an enumeration that includes the following enumerated values:

| Value | Description |
| --- | --- |
| unobtanium | The component under test will do the impossible. Require this feature to always skip a test. |
| ack-ordering | The component under test will order ACKs in accordance with MQTT protocol requirements. |
| reconnection | The component under test will automatically reconnect after a disconnection occurs. |
| caching | The component under test will cache Command responses for deduplication and reuse. |
| dispatch | The component under test will dispatch execution functions to a thread pool. |
| explicit-default | The component under test uses an explicit indication (not a sentinel value) to imply a default value. |

The remainder of this document defines and exemplifies the subsets of METL used for [`CommandExecutor`](#commandexecutor-test-suite), [`CommandInvoker`](#commandinvoker-test-suite), [`TelemetryReceiver`](#telemetryreceiver-test-suite), and [`TelemetrySender`](#telemetrysender-test-suite) test cases.
A final section describes [common test elements](#common-test-elements) that are usable across test cases.

## CommandExecutor test suite

The DTDL that defines request and response types for testing the `CommandExecutor` is as follows.

```json
{
  "@context": [
    "dtmi:dtdl:context;3",
    "dtmi:dtdl:extension:mqtt;1"
  ],
  "@id": "dtmi:test:TestModel;1",
  "@type": [ "Interface", "Mqtt" ],
  "payloadFormat": "Json/ecma/404",
  "commandTopic": "test/command/{commandName}",
  "contents": [
    {
      "@type": "Command",
      "name": "test",
      "request": {
        "name": "payload",
        "schema": {
          "@type": "Object",
          "fields": [
            {
              "name": "testCaseIndex",
              "schema": "integer"
            },
            {
              "name": "request",
              "schema": "string"
            }
          ]
        }
      },
      "response": {
        "name": "payload",
        "schema": {
          "@type": "Object",
          "fields": [
            {
              "name": "testCaseIndex",
              "schema": "integer"
            },
            {
              "name": "response",
              "schema": "string"
            }
          ]
        }
      }
    }
  ]
}
```

Because tests can execute concurrently, and the response cache is shared across all `CommandExecutor` instances, it is necessary to prevent test cases from interfering with each other.
The test engine generates a unique integer for each executing test case and populates the `testCaseIndex` field of the request payload with this value.
This ensures that separate test cases do not generate identical requests, which could collide in the cache.

In the `CommandExecutor` execution function, the test engine copies the `testCaseIndex` from the request payload to the response payload.
Whenever a test case indicates that a response value should be checked, the test engine also checks the `testCaseIndex` value to ensure it matches the value for the test case.

### CommandExecutor test language

The YAML file for a `CommandExecutor` test case can have the following top-level keys.

| Key | Required | Value Type | Description |
| --- | --- | --- | --- |
| test-name | yes | string | The name of the test case, usually matches the file name without extension. |
| description | yes | Description | English description of the test case. |
| requires | no | array of [FeatureKind](#featurekind) | List of features required by the test case. |
| prologue | yes | [ExecutorPrologue](#executorprologue) | Initialization to perform prior to stepping through the test-case actions. |
| actions | no | array of [ExecutorAction](#executoraction) | A sequence of actions to perform. |
| epilogue | no | [ExecutorEpilogue](#executorepilogue) | Finalization to perform after stepping through the test-case actions. |

The `test-name`, `aka`, and `descriptions` keys are to assist human readability.
The `requires` key is described above in the introduction to this document.
The `prologue`, `actions`, and `epilogue` keys define the three main regions of the test case.
These regions are detailed below, beginning with the simpler prologue and epilogue regions, followed by the set of supported actions.

### CommandExecutor test prologue

The prologue defines initialization to perform prior to stepping through any test-case actions.
This includes configuring the MQTT client, instantiating one or more CommandExecutors, and preparing synchronization items for use in the test.
The prologue can also define an expectation of error behavior when the configuration or initialization is intentionally invalid.
Following is an example CommandExecutor prologue:

```yaml
prologue:
  executors:
  - execution-timeout: { seconds: 0 }
  catch:
    error-kind: invalid configuration
    in-application: !!bool false
    is-shallow: !!bool true
    is-remote: !!bool false 
    supplemental:
      property-name: 'executiontimeout'
```

When a `catch` key is present in a prologue, the test stops after the exception/error is generated, so there is no need for further test-case regions.

#### ExecutorPrologue

A CommandExecutor prologue can have the following child keys:

| Key | Test Kind | Required | Value Type | Description |
| --- | --- | --- | --- | --- |
| mqtt-config | drive | no | [MqttConfig](#mqttconfig) | MQTT client configuration settings. |
| push-acks | drive | no | [PushAcks](#pushacks) | Queues of ACKs that are used sequentially to respond to various asynchronous MQTT messages. |
| executors | drive | no | array of [Executor](#executor) | A list of CommandExecutor instances to initialize for use in the test. |
| catch | check | no | [Catch](#catch) | An error that is expected to be caught during initialization. |
| countdown-events | drive | no | map from string to integer | Names and initial values of countdown events that can be signaled and awaited during the test. |

The value types for `mqtt-config`, `push-acks`, and `catch` are common across classes, so they are defined towards the end of this document.
The value type for `executors` is specific to CommandExecutor and is defined in the next subsection.

The value of `countdown-events` is a map that defines a collection of named countdown events and their initial values.
Each of these events may be signaled and/or awaited by a CommandExecutor instance and/or by a test action.
An example usage follows:

```yaml
prologue:
  countdown-events:
    'pass': 1
  executors:
  - command-name: "waiter"
    request-topic: "mock/waiter"
    sync:
    - wait-event: 'pass'
  - command-name: "runner"
    request-topic: "mock/runner"
```

#### Executor

Each element of the `executors` array can have the following child keys:

| Key | Test Kind | Required | Value Type | Default Value | Description |
| --- | --- | --- | --- | --- | --- |
| command-name | drive | no | string or null | "test" | The name of the Command. |
| request-topic | drive | no | string or null | "mock/test" | The MQTT topic pattern for the Command request. |
| model-id | drive | no | string or null | "dtmi:test:MyModel;1" | The identifier of the the service model, which is the full DTMI of the Interface. |
| executor-id | drive | no | string or null | "someExecutor" | Identifier of the asset that is targeted to execute a Command. |
| topic-namespace | drive | no | string or null | null | A leading namespace for the Command request MQTT topic pattern. |
| custom-token-map | drive | no | map from string to string | { } | A map from custom topic tokens to replacement values. |
| idempotent | drive | no | boolean | False | Whether it is permissible to execute the Command multiple times for a single invocation of the Command. |
| cache-ttl | drive | no | [Duration](#duration) or null | { "seconds": 0 } | Maximum duration for which a response to a Command instance may be reused as a response to other Command instances. |
| execution-timeout | drive | no | [Duration](#duration) or null | { "seconds": 10 } | Maximum duration to permit a Command to execute before aborting the execution. |
| request-responses-map | drive | no | map from string to array of string | { "Test_Request": [ "Test_Response" ] } | A map from received request value to an array of response values to be used sequentially. |
| response-metadata | drive | no | map from string to string or null | { } | Keys and values for header fields to be set in the Command response; a null value should be replaced from the matching key in the Command request. |
| execution-concurrency | drive | no | integer or null | null | A limit on the count of concurrent executions to reqest from the command dispatcher. |
| raise-error | drive | no | [Error](#error) |  | Raise an error from the Command execution function. |
| sync | drive | no | array of [Sync](#sync) | [ ] | A sequence of synchronization operations to perform during execution of the Command. |

The value of `request-responses-map` is used to emulate a user-code command execution function.
When a request is received, its value is looked up in the map, and a value from the mapped array is used as the response.
The values in each array are used in sequence, wrapping if the count of request instances exceeds the array length.
If the request value is not found in the map, or if the mapped array has no elements, a null response is used.

The value type for `cacheable-duration` and `executor-timeout` is common across classes, so it is defined towards the end of this document.
The value types for `raise-error` and `sync` are specific to CommandExecutor and are defined in the next subsections.

#### Error

The 'raise-error' key causes the CommandExecutor's execution function to raise an error, as in the following example:

```yaml
  - raise-error:
      kind: content
      message: "This is a content error with details"
      property-name: "requestHeader"
      property-value: "requestValue"
```

The Error can have the following child keys:

| Key | Test Kind | Required | Value Type | Description |
| --- | --- | --- | --- | --- |
| kind | drive | yes | [ErrorKind](#errorkind) | The kind of error to raise. |
| message | drive | no | string | The error message. |
| property-name | drive | no | string | The name of the property that is invalid; only used for content errors. |
| property-value | drive | no | string | The value of the property that is invalid; only used for content errors. |

The error kind is defined in the next subsection.

#### ErrorKind

The error kind includes the following enumerated values:

| Value | Description |
| --- | --- |
| none | No error. |
| content | User code identified an error in the request. |
| execution | User code encountered an error while executing the command. |

#### Sync

The `sync` key causes the CommandExecutor to perform a sequence of synchronization operations with one or more countdown events, as in the following example:

```yaml
    sync:
    - signal-event: 'barrier'
    - wait-event: 'barrier'
```

The synchronization operations in the array are executed in order, and the command execution function will not complete until all sync operations have executed.
Although it is possible for a single array element to contain keys for both signaling and waiting, by convention only one of these operations is indicated per element, to make their relative ordering explicit.
Each element of the `sync` array can have the following child keys:

| Key | Test Kind | Required | Value Type | Description |
| --- | --- | --- | --- | --- |
| signal-event | drive | no | string | Name of a countdown event to signal. |
| wait-event | drive | no | string | Name of a countdown event to await. |

### CommandExecutor test epilogue

The epilogue defines finalization to perform after stepping through any test-case actions.
This mainly involves checking to ensure that various things have happened as they should have.
This includes MQTT subscriptions, publications, and acknowledgements, as well as executions of the user callback code.
The epilogue can also define an expectation of error behavior during finalization.
Following is an example CommandExecutor epilogue:

```yaml
epilogue:
  published-messages:
  - correlation-index: 0
    topic: "response/mock/test"
    command-status: 505 # Not Supported Version
    is-application-error: false
    metadata:
      "__supProtMajVer": "0"
      "__requestProtVer": "this is not a valid protocol version"
      "__protVer": "0.1"
```

#### ExecutorEpilogue

A CommandExecutor epilogue can have the following child keys:

| Key | Test Kind | Required | Value Type | Description |
| --- | --- | --- | --- | --- |
| subscribed-topics | check | no | array of string | A list of MQTT topics that have been subscribed. |
| publication-count | check | no | integer | The count of messages published. |
| published-messages | check | no | array of [PublishedResponse](#publishedresponse) | A list of response messages published. |
| acknowledgement-count | check | no | integer | The count of acknowledgements sent. |
| execution-count | check | no | integer | For a single executor, the number of times the execution function has run. |
| execution-counts | check | no | map from integer to integer | For multiple executors, a map from the executor's index to the number of times its execution function has run. |
| catch | check | no | [Catch](#catch) | An error that is expected to be caught during finalization. |

The value type for `catch` is common across classes, so it is defined towards the end of this document.
The value type for `published-messages` is specific to CommandExecutor and is defined in the next subsection.

#### PublishedResponse

Each element of the `published-messages` array can have the following child keys:

| Key | Test Kind | Required | Value Type | Description |
| --- | --- | --- | --- | --- |
| correlation-index | match | yes | integer or null | An arbitrary numeric value used to identify the correlation ID used in request and response messages; null matches singular absent header property. |
| topic | check | no | string | The MQTT topic to which the message is published. |
| payload | check | no | string or null | The UTF8 string encapsulated in the response payload, or null if no payload. |
| metadata | check | no | map from string to string or null | Keys and values of header fields in the message; a null value indicates field should not be present. |
| command-status | check | no | integer or null | HTTP status code in the message, or null if no status code present. |
| is-application-error | check | no | boolean | In an error response, whether the error is in the application rather than in the platform. |
| expiry | check | no | integer | The message expiry in seconds. |

The value for `correlation-index` is an arbitrary number that will be given a replacement values by the test engine.
The index value can be used in multiple actions and in the epilogue, and each value will maintain a consistent replacement for the entirety of the test.

### CommandExecutor test actions

The actions define a sequence of test operations to perform.
Following is an example CommandExecutor actions array:

```yaml
actions:
- action: receive request
  correlation-index: 0
- action: receive request
  correlation-index: 1
- action: receive request
  correlation-index: 2
- action: await acknowledgement
- action: await acknowledgement
- action: await acknowledgement
```

#### ExecutorAction

The elements in a CommandExecutor action array have polymorphic types, each of which defines a specific test action, as indicated by the following table:

| Action | Subtype | Description |
| --- | --- | --- |
| receive request | [ActionReceiveRequest](#actionreceiverequest) | Receive a request message. |
| await publish | [ActionAwaitPublishResponse](#actionawaitpublishresponse) | Wait for the publication of a response message. |
| sync | [ActionSync](#actionsync) | Synchronize with a countdown event. |
| await acknowledgement | [ActionAwaitAck](#actionawaitack) | Wait for a received message to be acknowledged. |
| disconnect | [ActionDisconnect](#actiondisconnect) | Disconnect the MQTT client from the broker. |
| sleep | [ActionSleep](#actionsleep) | Sleep for a specified duration. |
| freeze time | [ActionFreezeTime](#actionfreezetime) | Freeze time so the clock does not advance. |
| unfreeze time | [ActionUnfreezeTime](#actionunfreezetime) | Unfreeze time so the clock resumes normal advancement. |

The details of actions `await acknowledgement`, `disconnect`, `sleep`, `freeze time`, and `unfreeze time` are common across classes, so they are defined towards the end of this document.
The details of actions `receive request`, `await publish`, and `sync` are described in the following subsections.

#### ActionReceiveRequest

A `receive request` action causes the CommandExecutor to receive a request message, as in the following example:

```yaml
- action: receive request
  topic: "mock/waiter"
  correlation-index: 0
  message-expiry: { seconds: 10 }
  response-topic: "mock/waiter/response"
  packet-index: 0
```

When the value of the `action` key is `receive request`, the following sibling keys are also available:

| Key | Test Kind | Required | Value Type | Value | Default Value | Description |
| --- | --- | --- | --- | --- | --- | --- |
| action |  | yes | string | "receive request" |  | Receive a request message. |
| topic | drive | no | string |  | "mock/test" | The MQTT topic on which the message is published. |
| payload | drive | no | string or null |  | "Test_Request" | A UTF8 string to encapsulate in the request payload; if null, omit payload from request message. |
| bypass-serialization | drive | no | boolean |  | false | Bypass serializing the payload and just embed raw bytes. |
| content-type | drive | no | string or null |  | "application/json" | The value of the ContentType header in the message, or null if no such header. |
| format-indicator | drive | no | integer or null |  | 1 | The value of the PayloadFormatIndicator header in the message, or null if no such header. |
| metadata | drive | no | map from string to string |  | { } | Keys and values for header fields in the message. |
| correlation-index | drive | no | integer or null |  | 0 | An arbitrary numeric value used to identify the correlation ID in the message; null omits correlation ID in header. |
| correlation-id | drive | no | string |  |  | A specific value for the correlation ID in the message; should be omitted except when testing correlation ID validity. |
| qos | drive | no | integer |  | 1 | MQTT QoS level. |
| message-expiry | drive | no | [Duration](#duration) or null |  | { "seconds": 10 } | Maximum duration for which a response remains desired by the requester. |
| response-topic | drive | no | string or null |  | "response/mock/test" | The MQTT topic pattern to which the Command response should be published. |
| source-index | drive | no | integer or null |  | 0 | An arbitrary numeric value used to identify the CommandInvoker that sent the request; null omits source ID in header. |
| packet-index | match | no | integer |  |  | An arbitrary numeric value used to identify the packet ID in the message. |

Values for `correlation-index`, `source-index`, and `packet-index` are arbitrary numbers that will be given replacement values by the test engine.
The index values can be used in multiple actions and in the epilogue, and each value will maintain a consistent replacement for the entirety of the test.

#### ActionAwaitPublishResponse

An `await publish` action causes the test system to wait for the CommandExecutor to publish a response message, as in the following example:

```yaml
- action: await publish
  correlation-index: 0
```

When the value of the `action` key is `await publish`, the following sibling keys are also available:

| Key | Test Kind | Required | Value Type | Value | Description |
| --- | --- | --- | --- | --- | --- |
| action |  | yes | string | "await publish" | Wait for the publication of a response message. |
| correlation-index | check | no | integer |  | An arbitrary numeric value used to identify the correlation ID in the message. |

The value for `correlation-index` is an arbitrary number that corresponds to a replacement value given by the test engine.
The replacement value is checked against the correlation ID in the published response message.

#### ActionSync

A `sync` action causes the test system to synchronize with a countdown event, as in the following example:

```yaml
- action: sync
  signal-event: 'pass'
```

Although it is possible for a single action to contain keys for both signaling and waiting, by convention only one of these operations is indicated per action, to make their relative ordering explicit.
When the value of the `action` key is `await publish`, the following sibling keys are also available:

| Key | Test Kind | Required | Value Type | Value | Description |
| --- | --- | --- | --- | --- | --- |
| action |  | yes | string | "sync" | Synchronize with a countdown event. |
| signal-event | drive | no | string |  | Name of a countdown event to signal. |
| wait-event | drive | no | string |  | Name of a countdown event to await. |

## CommandInvoker test suite

Request and response types of `string` are used for testing the `CommandInvoker`.
There are no shared components across `CommandInvoker` instances, so no special techniques are necessary to prevent test cases from interfering with each other.

### CommandInvoker test language

The YAML file for a `CommandInvoker` test case can have the following top-level keys.

| Key | Required | Value Type | Description |
| --- | --- | --- | --- |
| test-name | yes | string | The name of the test case, usually matches the file name without extension. |
| description | yes | Description | English description of the test case. |
| requires | no | array of [FeatureKind](#featurekind) | List of features required by the test case. |
| prologue | yes | [InvokerPrologue](#invokerprologue) | Initialization to perform prior to stepping through the test-case actions. |
| actions | no | array of [InvokerAction](#invokeraction) | A sequence of actions to perform. |
| epilogue | no | [InvokerEpilogue](#invokerepilogue) | Finalization to perform after stepping through the test-case actions. |

The `test-name`, `aka`, and `descriptions` keys are to assist human readability.
The `requires` key is described above in the introduction to this document.
The `prologue`, `actions`, and `epilogue` keys define the three main regions of the test case.
These regions are detailed below, beginning with the simpler prologue and epilogue regions, followed by the set of supported actions.

### CommandInvoker test prologue

The prologue defines initialization to perform prior to stepping through any test-case actions.
This includes configuring the MQTT client and instantiating one or more CommandInvokers.
The prologue can also define an expectation of error behavior when the configuration or initialization is intentionally invalid.
Following is an example CommandInvoker prologue:

```yaml
prologue:
  push-acks:
    subscribe: [ fail ]
  invokers:
  - { }
  catch:
    error-kind: mqtt error
    in-application: !!bool false
    is-shallow: !!bool false
    is-remote: !!bool false 
```

When a `catch` key is present in a prologue, the test stops after the exception/error is generated, so there is no need for further test-case regions.

#### InvokerPrologue

A CommandInvoker prologue can have the following child keys:

| Key | Test Kind | Required | Value Type | Description |
| --- | --- | --- | --- | --- |
| mqtt-config | drive | no | [MqttConfig](#mqttconfig) | MQTT client configuration settings. |
| push-acks | drive | no | [PushAcks](#pushacks) | Queues of ACKs that are used sequentially to respond to various asynchronous MQTT messages. |
| invokers | drive | no | array of [Invoker](#invoker) | A list of CommandInvoker instances to initialize for use in the test. |
| catch | check | no | [Catch](#catch) | An error that is expected to be caught during initialization. |

The value types for `mqtt-config`, `push-acks`, and `catch` are common across classes, so they are defined towards the end of this document.
The value type for `invokers` is specific to CommandInvoker and is defined in the next subsection.

#### Invoker

Each element of the `invokers` array can have the following child keys:

| Key | Test Kind | Required | Value Type | Default Value | Description |
| --- | --- | --- | --- | --- | --- |
| command-name | drive | no | string or null | "test" | The name of the Command. |
| request-topic | drive | no | string or null | "mock/test" | The MQTT topic pattern for the Command request. |
| model-id | drive | no | string or null | "dtmi:test:MyModel;1" | The identifier of the the service model, which is the full DTMI of the Interface. |
| topic-namespace | drive | no | string or null | null | A leading namespace for the Command request and response MQTT topic patterns. |
| response-topic-prefix | drive | no | string or null | "response" | A prefix to be prepended to the request topic pattern to produce a response topic pattern. |
| response-topic-suffix | drive | no | string or null | null | A suffix to be appended to the request topic pattern to produce a response topic pattern. |
| custom-token-map | drive | no | map from string to string | { } | A map from custom topic tokens to replacement values. |
| response-topic-map | drive | no | map from string to string | { } | A map from request topic to response topic, as an alternative to using prefix/suffix. |

### CommandInvoker test epilogue

The epilogue defines finalization to perform after stepping through any test-case actions.
This mainly involves checking to ensure that various things have happened as they should have.
This includes MQTT subscriptions, publications, and acknowledgements.
The epilogue can also define an expectation of error behavior during finalization.
Following is an example CommandInvoker epilogue:

```yaml
epilogue:
  subscribed-topics:
  - "response/mock/test"
  acknowledgement-count: 2
  published-messages:
  - correlation-index: 0
    topic: "mock/test"
    payload: "Test_Request"
  - correlation-index: 1
    topic: "mock/test"
    payload: "Test_Request"
```

#### InvokerEpilogue

A CommandInvoker epilogue can have the following child keys:

| Key | Test Kind | Required | Value Type | Description |
| --- | --- | --- | --- | --- |
| subscribed-topics | check | no | array of string | A list of MQTT topics that have been subscribed. |
| publication-count | check | no | integer | The count of messages published. |
| published-messages | check | no | array of [PublishedRequest](#publishedrequest) | A list of request messages published. |
| acknowledgement-count | check | no | integer | The count of acknowledgements sent. |

The value type for `published-messages` is specific to CommandInvoker and is defined in the next subsection.

#### PublishedRequest

Each element of the `published-messages` array can have the following child keys:

| Key | Test Kind | Required | Value Type | Description |
| --- | --- | --- | --- | --- |
| correlation-index | match | yes | integer | An arbitrary numeric value used to identify the correlation ID used in request and response messages. |
| topic | check | no | string | The MQTT topic to which the message is published. |
| payload | check | no | string or null | The request payload UTF8 string, or null if no payload. |
| metadata | check | no | map from string to string or null | Keys and values of header fields in the message; a null value indicates field should not be present. |
| source-id | check | no | string | The source ID header property in the message. |
| expiry | check | no | integer | The message expiry in seconds. |

The value for `correlation-index` is an arbitrary number that will be given a replacement values by the test engine.
The index value can be used in multiple actions and in the epilogue, and each value will maintain a consistent replacement for the entirety of the test.

### CommandInvoker test actions

The actions define a sequence of test operations to perform.
Following is an example CommandInvoker actions array:

```yaml
actions:
- action: invoke command
  invocation-index: 0
- action: await publish
  correlation-index: 0
- action: receive response
  correlation-index: 0
  packet-index: 0
- action: await invocation
  invocation-index: 0
- action: await acknowledgement
  packet-index: 0
```

#### InvokerAction

The elements in a CommandInvoker action array have polymorphic types, each of which defines a specific test action, as indicated by the following table:

| Action | Subtype | Description |
| --- | --- | --- |
| invoke command | [ActionInvokeCommand](#actioninvokecommand) | Invoke a Command without waiting for its completion. |
| await invocation | [ActionAwaitInvocation](#actionawaitinvocation) | Wait for a Command invocation to complete. |
| await publish | [ActionAwaitPublishRequest](#actionawaitpublishrequest) | Wait for the publication of a request message. |
| receive response | [ActionReceiveResponse](#actionreceiveresponse) | Receive a response message. |
| await acknowledgement | [ActionAwaitAck](#actionawaitack) | Wait for a received message to be acknowledged. |
| disconnect | [ActionDisconnect](#actiondisconnect) | Disconnect the MQTT client from the broker. |
| sleep | [ActionSleep](#actionsleep) | Sleep for a specified duration. |
| freeze time | [ActionFreezeTime](#actionfreezetime) | Freeze time so the clock does not advance. |
| unfreeze time | [ActionUnfreezeTime](#actionunfreezetime) | Unfreeze time so the clock resumes normal advancement. |

The details of actions `await acknowledgement`, `disconnect`, `sleep`, `freeze time`, and `unfreeze time` are common across classes, so they are defined towards the end of this document.
The details of actions `invoke command`, `await invocation`, `await publish`, and `receive response` are described in the following subsections.

#### ActionInvokeCommand

An `invoke command` action causes the CommandInvoker to invoke a command without waiting for its completion, as in the following example:

```yaml
- action: invoke command
  invocation-index: 0
  metadata:
    "__hasReservePrefix": "userValue"
```

When the value of the `action` key is `invoke command`, the following sibling keys are also available:

| Key | Test Kind | Required | Value Type | Value | Default Value | Description |
| --- | --- | --- | --- | --- | --- | --- |
| action |  | yes | string | "invoke command" |  | Invoke a Command without waiting for its completion. |
| invocation-index | match | yes | integer |  |  | An arbitrary numeric value used to identify the invocation. |
| command-name | drive | no | string |  | "test" | The name of the Command. |
| executor-id | drive | no | string |  | "someExecutor" | Identifier of the asset that is targeted to execute a Command. |
| timeout | drive | no | [Duration](#duration) or null |  | { "minutes": 1 } | Command timeout duration. |
| request-value | drive | no | string or null |  | "Test_Request" | A UTF8 string (or null) value for the Command request. |
| metadata | drive | no | map from string to string |  | { } | Keys and values for user metadata. |

The value for `invocation-index` is an arbitrary number that will be given a replacement values by the test engine.
The index value can be used in multiple actions, and each value will maintain a consistent replacement for the entirety of the test.

#### ActionAwaitInvocation

An `await invocation` action causes the test system to wait for a command invocation to complete, as in the following example:

```yaml
- action: await invocation
  invocation-index: 0
  catch:
    error-kind: internal logic error
    in-application: !!bool false
    is-shallow: !!bool false
    is-remote: !!bool true
    status-code: 500
    supplemental:
      property-name: 'buffer'
```

When the value of the `action` key is `await invocation`, the following sibling keys are also available:

| Key | Test Kind | Required | Value Type | Value | Description |
| --- | --- | --- | --- | --- | --- |
| action |  | yes | string | "await invocation" | Wait for a Command invocation to complete. |
| invocation-index | match | yes | integer |  | An arbitrary numeric value used to identify the invocation. |
| catch | check | no | [Catch](#catch) |  | An error that is expected to be caught during Command invocation. |
| response-value | check | no | string or null |  | A UTF8 string (or null) value expected for the Command response. |
| metadata | check | no | map from string to string |  | Keys and values for response user metadata. |

The value for `invocation-index` is an arbitrary number that will be given a replacement values by the test engine.
The index value can be used in multiple actions, and each value will maintain a consistent replacement for the entirety of the test.

#### ActionAwaitPublishRequest

An `await publish` action causes the test system to wait for the CommandInvoker to publish a request message, as in the following example:

```yaml
- action: await publish
  correlation-index: 0
```

When the value of the `action` key is `await publish`, the following sibling keys are also available:

| Key | Test Kind | Required | Value Type | Value | Description |
| --- | --- | --- | --- | --- | --- |
| action |  | yes | string | "await publish" | Wait for the publication of a request message. |
| correlation-index | match | no | integer |  | An arbitrary numeric value used to identify the correlation ID in the message. |

The value for `correlation-index` is an arbitrary number.
Unlike index values for the CommandExecutor, and unlike other index values for the CommandInvoker, the correlation index is not given a replacement values by the test engine.
Instead, it is mapped to the correlation identifier assigned by the CommandInvoker itself, which is embedded in the request message.
Therefore, the correlation index must be used in an `await publish` action before it is used in any other action or in the epilogue.
Thereafter, the identifier assigned by the CommandInvoker can be referenced in the remainder of the test.

#### ActionReceiveResponse

A `receive response` action causes the CommandInvoker to receive a response message, as in the following example:

```yaml
- action: receive response
  correlation-index: 0
  status: "505" # Not Supported Version
  is-application-error: "false"
  metadata:
    "__supProtMajVer": "2 3 4"
    "__requestProtVer": "0.1"
    "__protVer": "0.1"
    "__stMsg": "This is a not supported version exception"
```

When the value of the `action` key is `receive response`, the following sibling keys are also available:

| Key | Test Kind | Required | Value Type | Value | Default Value | Description |
| --- | --- | --- | --- | --- | --- | --- |
| action |  | yes | string | "receive response" |  | Receive a response message. |
| topic | drive | no | string |  | "response/mock/test" | The MQTT topic on which the message is published. |
| payload | drive | no | string or null |  | "Test_Response" | A UTF8 string for the request payload; if null, omit payload from request message. |
| bypass-serialization | drive | no | boolean |  | false | Bypass serializing the payload and just embed raw bytes. |
| content-type | drive | no | string or null |  | "application/json" | The value of the ContentType header in the message, or null if no such header. |
| format-indicator | drive | no | integer or null |  | 1 | The value of the PayloadFormatIndicator header in the message, or null if no such header. |
| metadata | drive | no | map from string to string |  | { } | Keys and values for header fields in the message. |
| correlation-index | drive | no | integer or null |  | 0 | An arbitrary numeric value used to identify the correlation ID in the message; null omits correlation ID in header. |
| qos | drive | no | integer |  | 1 | MQTT QoS level. |
| message-expiry | drive | no | [Duration](#duration) or null |  | { "seconds": 10 } | Maximum duration for which a response remains desired by the requester. |
| status | drive | no | string or null |  | "200" | HTTP status code. |
| status-message | drive | no | string or null |  | null | Human-readable status message. |
| is-application-error | drive | no | string or null |  | null | Nominally boolean value indicating whether a non-200 status is an application-level error. |
| invalid-property-name | drive | no | string or null |  | null | The name of an MQTT property in a request header that is missing or has an invalid value. |
| invalid-property-value | drive | no | string or null |  | null | The value of an MQTT property in a request header that is invalid. |
| packet-index | match | no | integer |  |  | An arbitrary numeric value used to identify the packet ID in the message. |

The value for `correlation-index` is an arbitrary number.
Unlike index values for the CommandExecutor, and unlike other index values for the CommandInvoker, the correlation index is not given a replacement values by the test engine.
Instead, it is mapped to the correlation identifier assigned by the CommandInvoker itself, which is embedded in the request message.
Therefore, the correlation index must have been used in an `await publish` action before it can be used in a `receive response` action.

## TelemetryReceiver test suite

A Telemetry type of `string` is used for testing the `TelemetryReceiver`.
There are no shared components across `TelemetryReceiver` instances, so no special techniques are necessary to prevent test cases from interfering with each other.

### TelemetryReceiver test language

The YAML file for a `TelemetryReceiver` test case can have the following top-level keys.

| Key | Required | Value Type | Description |
| --- | --- | --- | --- |
| test-name | yes | string | The name of the test case, usually matches the file name without extension. |
| description | yes | Description | English description of the test case. |
| requires | no | array of [FeatureKind](#featurekind) | List of features required by the test case. |
| prologue | yes | [ReceiverPrologue](#receiverprologue) | Initialization to perform prior to stepping through the test-case actions. |
| actions | no | array of [ReceiverAction](#receiveraction) | A sequence of actions to perform. |
| epilogue | no | [ReceiverEpilogue](#receiverepilogue) | Finalization to perform after stepping through the test-case actions. |

The `test-name`, `aka`, and `descriptions` keys are to assist human readability.
The `requires` key is described above in the introduction to this document.
The `prologue`, `actions`, and `epilogue` keys define the three main regions of the test case.
These regions are detailed below, beginning with the simpler prologue and epilogue regions, followed by the set of supported actions.

### TelemetryReceiver test prologue

The prologue defines initialization to perform prior to stepping through any test-case actions.
This includes configuring the MQTT client and instantiating one or more TelemetryReceivers.
The prologue can also define an expectation of error behavior when the configuration or initialization is intentionally invalid.
Following is an example TelemetryReceiver prologue:

```yaml
prologue:
  push-acks:
    subscribe: [ fail ]
  receivers:
  - { }
  catch:
    error-kind: mqtt error
    in-application: !!bool false
    is-shallow: !!bool false
    is-remote: !!bool false 
```

When a `catch` key is present in a prologue, the test stops after the exception/error is generated, so there is no need for further test-case regions.

#### ReceiverPrologue

A TelemetryReceiver prologue can have the following child keys:

| Key | Test Kind | Required | Value Type | Description |
| --- | --- | --- | --- | --- |
| mqtt-config | drive | no | [MqttConfig](#mqttconfig) | MQTT client configuration settings. |
| push-acks | drive | no | [PushAcks](#pushacks) | Queues of ACKs that are used sequentially to respond to various asynchronous MQTT messages. |
| receivers | drive | no | array of [Receiver](#receiver) | A list of TelemetryReceiver instances to initialize for use in the test. |
| catch | check | no | [Catch](#catch) | An error that is expected to be caught during initialization. |

The value types for `mqtt-config`, `push-acks`, and `catch` are common across classes, so they are defined towards the end of this document.
The value type for `receivers` is specific to TelemetryReceiver and is defined in the next subsection.

#### Receiver

Each element of the `receivers` array can have the following child keys:

| Key | Test Kind | Required | Value Type | Default Value | Description |
| --- | --- | --- | --- | --- | --- |
| telemetry-name | drive | no | string or null | "test" | The name of the Telemetry. |
| telemetry-topic | drive | no | string or null | "mock/test" | The MQTT topic pattern for the Telemetry. |
| model-id | drive | no | string or null | "dtmi:test:MyModel;1" | The identifier of the the service model, which is the full DTMI of the Interface. |
| topic-namespace | drive | no | string or null | null | A leading namespace for the Telemetry MQTT topic patterns. |
| custom-token-map | drive | no | map from string to string | { } | A map from custom topic tokens to replacement values. |
| raise-error | drive | no | [Error](#error) |  | Raise an error from the Telemetry receive function. |

### TelemetryReceiver test epilogue

The epilogue defines finalization to perform after stepping through any test-case actions.
This mainly involves checking to ensure that various things have happened as they should have.
This includes MQTT subscriptions, publications, and acknowledgements.
The epilogue can also define an expectation of error behavior during finalization.
Following is an example TelemetryReceiver epilogue:

```yaml
epilogue:
  telemetry-count: 3
  subscribed-topics:
  - "mock/test"
  acknowledgement-count: 3
  received-telemetries:
  - telemetry-value: "Test_Telemetry"
  - telemetry-value: "Test_Telemetry"
  - telemetry-value: "Test_Telemetry"
```

#### ReceiverEpilogue

A TelemetryReceiver epilogue can have the following child keys:

| Key | Test Kind | Required | Value Type | Description |
| --- | --- | --- | --- | --- |
| subscribed-topics | check | no | array of string | A list of MQTT topics that have been subscribed. |
| acknowledgement-count | check | no | integer | The count of acknowledgements sent. |
| telemetry-count | check | no | integer | For a single receiver, the number of telemetries received. |
| telemetry-counts | check | no | map from integer to integer | For multiple receivers, a map from the receiver's index to the number of Telemetries received. |
| received-telemetries | check | no | array of [ReceivedTelemetry](#receivedtelemetry) | An ordered list of Telemetries received. |
| catch | check | no | [Catch](#catch) | An error that is expected to be caught during finalization. |

The value type for `received-telemetries` is specific to TelemetryReceiver and is defined in the next subsection.

#### ReceivedTelemetry

Each element of the `received-telemetries` array can have the following child keys:

| Key | Test Kind | Required | Value Type | Description |
| --- | --- | --- | --- | --- |
| telemetry-value | check | no | string or null | A UTF8 string (or null) value expected for the Telemetry content. |
| metadata | check | no | map from string to string or null | Keys and values of expected metadata; a null value indicates key should not be present. |
| cloud-event | check | no | [ReceivedCloudEvent](#receivedcloudevent) | A CloudEvent expected to be associated with the Telemetry. |
| source-index | check | no | integer | An arbitrary numeric value used to identify the TelemetrySender that sent the telemetry. |

The order of messasges in the `received-telemetries` array matches the expected order in which the telemetries are to be relayed to user code.
The value type for `cloud-event` is defined in the next subsection.

#### ReceivedCloudEvent

The cloud event can have the following child keys:

| Key | Test Kind | Required | Value Type | Description |
| --- | --- | --- | --- | --- |
| source | check | no | string | URI that identifies the context in which an event happened. |
| type | check | no | string | The type of event related to the originating occurrence. |
| spec-version | check | no | string | The version of the CloudEvents specification which the event uses. |
| data-content-type | check | no | string | The content type of the data value. |
| subject | check | no | string | The subject of the event in the context of the event producer. |
| data-schema | check | no | string | URI that identifies the schema the data adheres to. |

### TelemetryReceiver test actions

The actions define a sequence of test operations to perform.
Following is an example TelemetryReceiver actions array:

```yaml
actions:
- action: receive telemetry
  packet-index: 0
- action: await acknowledgement
  packet-index: 0
- action: receive telemetry
  packet-index: 0
- action: await acknowledgement
  packet-index: 0
```

#### ReceiverAction

The elements in a TelemetryReceiver action array have polymorphic types, each of which defines a specific test action, as indicated by the following table:

| Action | Subtype | Description |
| --- | --- | --- |
| receive telemetry | [ActionReceiveTelemetry](#actionreceivetelemetry) | Receive a telemetry message. |
| await acknowledgement | [ActionAwaitAck](#actionawaitack) | Wait for a received message to be acknowledged. |
| disconnect | [ActionDisconnect](#actiondisconnect) | Disconnect the MQTT client from the broker. |
| sleep | [ActionSleep](#actionsleep) | Sleep for a specified duration. |
| freeze time | [ActionFreezeTime](#actionfreezetime) | Freeze time so the clock does not advance. |
| unfreeze time | [ActionUnfreezeTime](#actionunfreezetime) | Unfreeze time so the clock resumes normal advancement. |

The details of actions `await acknowledgement`, `disconnect`, `sleep`, `freeze time`, and `unfreeze time` are common across classes, so they are defined towards the end of this document.
The details of action `receive telemetry` are described in the following subsection.

#### ActionReceiveTelemetry

A `receive telemetry` action causes the TelemetryReceiver to receive a telemetry message, as in the following example:

```yaml
- action: receive telemetry
  metadata:
    "id": "dtmi:test:someAssignedId;1"
    "source": "dtmi:test:myEventSource;1"
    "type": "test-type"
    "specversion": "1.0"
    "datacontenttype": "application/json"
    "subject": "mock/test"
    "dataschema": "dtmi:test:MyModel:_contents:__test;1"
  packet-index: 0
```

When the value of the `action` key is `receive telemetry`, the following sibling keys are also available:

| Key | Test Kind | Required | Value Type | Value | Default Value | Description |
| --- | --- | --- | --- | --- | --- | --- |
| action |  | yes | string | "receive telemetry" |  | Receive a telemetry message. |
| topic | drive | no | string |  | "mock/test" | The MQTT topic on which the message is published. |
| payload | drive | no | string or null |  | "Test_Telemetry" | A UTF8 string to encapsulate in the telemetry payload; if null, omit payload from telemetry message. |
| bypass-serialization | drive | no | boolean |  | false | Bypass serializing the payload and just embed raw bytes. |
| content-type | drive | no | string or null |  | "application/json" | The value of the ContentType header in the message, or null if no such header. |
| format-indicator | drive | no | integer or null |  | 1 | The value of the PayloadFormatIndicator header in the message, or null if no such header. |
| metadata | drive | no | map from string to string |  | { } | Keys and values for header fields in the message. |
| qos | drive | no | integer |  | 1 | MQTT QoS level. |
| message-expiry | drive | no | [Duration](#duration) or null |  | { "seconds": 10 } | Maximum duration for which a response remains desired by the sender. |
| source-index | drive | no | integer or null |  | 0 | An arbitrary numeric value used to identify the TelemetrySender that sent the telemetry; null omits source ID in header. |
| packet-index | match | no | integer |  |  | An arbitrary numeric value used to identify the packet ID in the message. |

Values for `source-index` and `packet-index` are arbitrary numbers that will be given replacement values by the test engine.
The index values can be used in multiple actions and in the epilogue, and each value will maintain a consistent replacement for the entirety of the test.

## TelemetrySender test suite

A Telemetry type of `string` is used for testing the `TelemetrySender`.
There are no shared components across `TelemetrySender` instances, so no special techniques are necessary to prevent test cases from interfering with each other.

### TelemetrySender test language

The YAML file for a `TelemetrySender` test case can have the following top-level keys.

| Key | Required | Value Type | Description |
| --- | --- | --- | --- |
| test-name | yes | string | The name of the test case, usually matches the file name without extension. |
| description | yes | Description | English description of the test case. |
| requires | no | array of [FeatureKind](#featurekind) | List of features required by the test case. |
| prologue | yes | [SenderPrologue](#senderprologue) | Initialization to perform prior to stepping through the test-case actions. |
| actions | no | array of [SenderAction](#senderaction) | A sequence of actions to perform. |
| epilogue | no | [SenderEpilogue](#senderepilogue) | Finalization to perform after stepping through the test-case actions. |

The `test-name`, `aka`, and `descriptions` keys are to assist human readability.
The `requires` key is described above in the introduction to this document.
The `prologue`, `actions`, and `epilogue` keys define the three main regions of the test case.
These regions are detailed below, beginning with the simpler prologue and epilogue regions, followed by the set of supported actions.

### TelemetrySender test prologue

The prologue defines initialization to perform prior to stepping through any test-case actions.
This includes configuring the MQTT client and instantiating one or more TelemetrySenders.
The prologue can also define an expectation of error behavior when the configuration or initialization is intentionally invalid.
Following is an example TelemetrySender prologue:

```yaml
prologue:
  senders:
  - topic-namespace: "invalid/{modelId}"
  catch:
    error-kind: invalid configuration
    in-application: !!bool false
    is-shallow: !!bool true
    is-remote: !!bool false 
    supplemental:
      property-name: 'topicnamespace'
      property-value: "invalid/{modelId}"
```

When a `catch` key is present in a prologue, the test stops after the exception/error is generated, so there is no need for further test-case regions.

#### SenderPrologue

A TelemetrySender prologue can have the following child keys:

| Key | Test Kind | Required | Value Type | Description |
| --- | --- | --- | --- | --- |
| mqtt-config | drive | no | [MqttConfig](#mqttconfig) | MQTT client configuration settings. |
| push-acks | drive | no | [PushAcks](#pushacks) | Queues of ACKs that are used sequentially to respond to various asynchronous MQTT messages. |
| senders | drive | no | array of [Sender](#sender) | A list of TelemetrySender instances to initialize for use in the test. |
| catch | check | no | [Catch](#catch) | An error that is expected to be caught during initialization. |

The value types for `mqtt-config`, `push-acks`, and `catch` are common across classes, so they are defined towards the end of this document.
The value type for `senders` is specific to TelemetrySender and is defined in the next subsection.

#### Sender

Each element of the `senders` array can have the following child keys:

| Key | Test Kind | Required | Value Type | Default Value | Description |
| --- | --- | --- | --- | --- | --- |
| telemetry-name | drive | no | string or null | "test" | The name of the Telemetry. |
| telemetry-topic | drive | no | string or null | "mock/test" | The MQTT topic pattern for the Telemetry. |
| model-id | drive | no | string or null | "dtmi:test:MyModel;1" | The identifier of the the service model, which is the full DTMI of the Interface. |
| data-schema | drive | no | string or null | "dtmi:test:MyModel:_contents:__test;1" | The data schema to use in a cloud event when associated with the telemetry. |
| topic-namespace | drive | no | string or null | null | A leading namespace for the Telemetry MQTT topic patterns. |
| custom-token-map | drive | no | map from string to string | { } | A map from custom topic tokens to replacement values. |

### TelemetrySender test epilogue

The epilogue defines finalization to perform after stepping through any test-case actions.
This mainly involves checking to ensure that various things have happened as they should have.
This includes MQTT subscriptions, publications, and acknowledgements.
The epilogue can also define an expectation of error behavior during finalization.
Following is an example TelemetrySender epilogue:

```yaml
epilogue:
  published-messages:
  - topic: "mock/test"
    payload: "Test_Telemetry"
    metadata:
      "source": "dtmi:test:myEventSource;1"
      "type": "test-type"
      "specversion": "1.0"
      "datacontenttype": "application/json"
      "subject": "mock/test"
      "dataschema": "dtmi:test:MyModel:_contents:__test;1"
```

#### SenderEpilogue

A TelemetrySender epilogue can have the following child keys:

| Key | Test Kind | Required | Value Type | Description |
| --- | --- | --- | --- | --- |
| publication-count | check | no | integer | The count of messages published. |
| published-messages | check | no | array of [PublishedTelemetry](#publishedtelemetry) | An ordered list of Telemetry messages published. |

The value type for `published-messages` is specific to TelemetrySender and is defined in the next subsection.

#### PublishedTelemetry

Each element of the `published-messages` array can have the following child keys:

| Key | Test Kind | Required | Value Type | Description |
| --- | --- | --- | --- | --- |
| topic | check | no | string | The MQTT topic to which the message is published. |
| payload | check | no | string | The Telemetry payload UTF8 string. |
| metadata | check | no | map from string to string or null | Keys and values of header fields in the message; a null value indicates field should not be present. |
| source-id | check | no | string | The source ID header property in the message. |
| expiry | check | no | integer | The message expiry in seconds. |

The order of messasges in the `published-messages` array matches the expected order in which the messages are to be published.

### TelemetrySender test actions

The actions define a sequence of test operations to perform.
Following is an example TelemetrySender actions array:

```yaml
actions:
- action: send telemetry
- action: await publish
- action: await send
  catch:
    error-kind: mqtt error
    in-application: !!bool false
    is-shallow: !!bool false
    is-remote: !!bool false
```

#### SenderAction

The elements in a TelemetrySender action array have polymorphic types, each of which defines a specific test action, as indicated by the following table:

| Action | Subtype | Description |
| --- | --- | --- |
| send telemetry | [ActionSendTelemetry](#actionsendtelemetry) | Send a Telemetry without waiting for its completion. |
| await send | [ActionAwaitSend](#actionawaitsend) | Wait for the least recent Telemetry send to complete. |
| await publish | [ActionAwaitPublishTelemetry](#actionawaitpublishtelemetry) | Wait for the publication of a Telemetry message. |
| disconnect | [ActionDisconnect](#actiondisconnect) | Disconnect the MQTT client from the broker. |

The details of action `disconnect` is common across classes, so it is defined towards the end of this document.
The details of actions `send telemetry`, `await send`, and `await publish` are described in the following subsections.

#### ActionSendTelemetry

A `send telemetry` action causes the TelemetrySender to send a telemetry without waiting for its completion, as in the following example:

```yaml
- action: send telemetry
  cloud-event:
    source: "dtmi:test:myEventSource;1"
    type: "test-type"
    spec-version: "1.0"
```

When the value of the `action` key is `send telemetry`, the following sibling keys are also available:

| Key | Test Kind | Required | Value Type | Value | Default Value | Description |
| --- | --- | --- | --- | --- | --- | --- |
| action |  | yes | string | "send telemetry" |  | Send a Telemetry without waiting for its completion. |
| telemetry-name | drive | no | string |  | "test" | The name of the Telemetry. |
| timeout | drive | no | [Duration](#duration) or null |  | { "minutes": 1 } | Telemetry timeout duration. |
| telemetry-value | drive | no | string or null |  | "Test_Telemetry" | A UTF8 string (or null) value for the Telemetry content. |
| metadata | drive | no | map from string to string |  | { } | Keys and values for user metadata. |
| cloud-event | drive | no | [OriginatingCloudEvent](#originatingcloudevent) |  |  | A CloudEvent associated with the Telemetry. |
| qos | drive | no | integer |  | 1 | MQTT QoS level. |

The value type for `cloud-event` is specific to TelemetrySender and is defined in the next subsection.

#### OriginatingCloudEvent

The cloud event can have the following child keys:

| Key | Test Kind | Required | Value Type | Description |
| --- | --- | --- | --- | --- |
| source | drive | yes | string | URI that identifies the context in which an event happened. |
| type | drive | no | string | The type of event related to the originating occurrence. |
| spec-version | drive | no | string | The version of the CloudEvents specification which the event uses. |

#### ActionAwaitSend

An `await send` action causes the test system to wait for a telemetry send to complete, as in the following example:

```yaml
- action: await send
  catch:
    error-kind: mqtt error
    in-application: !!bool false
    is-shallow: !!bool false
    is-remote: !!bool false
```

When the value of the `action` key is `await send`, the following sibling keys are also available:

| Key | Test Kind | Required | Value Type | Value | Description |
| --- | --- | --- | --- | --- | --- |
| action |  | yes | string | "await send" | Wait for the least recent Telemetry send to complete. |
| catch | check | no | [Catch](#catch) |  | An error that is expected to be caught while sending Telemetry. |

#### ActionAwaitPublishTelemetry

An `await publish` action causes the test system to wait for the TelemetrySender to publish a telemetry message, as in the following example:

```yaml
- action: await publish
```

When the value of the `action` key is `await publish`, the following sibling keys are also available:

| Key | Test Kind | Required | Value Type | Value | Description |
| --- | --- | --- | --- | --- | --- |
| action |  | yes | string | "await publish" | Wait for the publication of a Telemetry message. |

## Common test elements

Several test elements are usable in multiple kinds of unit test cases.

### Common test actions

Several action subtypes are usable in multiple kinds of unit test cases.

#### ActionAwaitAck

An `await acknowledgement` action causes the test system to wait for the class under test to send an acknowledgement, as in the following example:

```yaml
- action: await acknowledgement
  packet-index: 0
```

When the value of the `action` key is `await acknowledgement`, the following sibling keys are also available:

| Key | Test Kind | Required | Value Type | Value | Description |
| --- | --- | --- | --- | --- | --- |
| action |  | yes | string | "await acknowledgement" | Wait for a received message to be acknowledged. |
| packet-index | check | no | integer |  | An arbitrary numeric value used to identify the packet ID in the ACK message. |

The value for `packet-index` is an arbitrary number that corresponds to a replacement value given by the test engine.
The replacement value is checked against the packet ID in the published acknowledgement.

#### ActionDisconnect

A `disconnect` action disconnects the class under test from the MQTT broker, as in the following example:

```yaml
- action: disconnect
```

When the value of the `action` key is `disconnect`, no sibling keys are available.

#### ActionSleep

A `sleep` action causes the test system to sleep for a specified duration, as in the following example:

```yaml
- action: sleep
  duration: { seconds: 3 }
```

When the value of the `action` key is `sleep`, the following sibling keys are also available:

| Key | Test Kind | Required | Value Type | Value | Description |
| --- | --- | --- | --- | --- | --- |
| action |  | yes | string | "sleep" | Sleep for a specified duration. |
| duration | drive | yes | [Duration](#duration) |  | Duration to sleep. |

#### ActionFreezeTime

A `freeze time` action freezes the test time so the clock does not advance, as in the following example:

```yaml
- action: freeze time
```

When the value of the `action` key is `freeze time`, no sibling keys are available.

#### ActionUnfreezeTime

An `unfreeze time` action unfreezes the test time so the clock resumes normal advancement, as in the following example:

```yaml
- action: unfreeze time
```

When the value of the `action` key is `unfreeze time`, no sibling keys are available.

### Common prologue value types

All test-suite prologues have keys `mqtt-config`, `push-acks`, and `catch`, which have value types defined in this section.

#### MqttConfig

The value of `mqtt-config` provides MQTT client configuration settings, as in the following example:

```yaml
  mqtt-config:
    client-id: "MyInvokerClientId"
```

The MQTT configuration can have the following child keys:

| Key | Test Kind | Required | Value Type | Default Value | Description |
| --- | --- | --- | --- | --- | --- |
| client-id | drive | no | string |  | The MQTT client ID. |

#### PushAcks

The value of `push-acks` is a collection of queues of ACKs that are used sequentially to respond to various asynchronous MQTT messages, as in the following example:

```yaml
  push-acks:
    publish: [ drop ]
```

By convention, these arrays are written in YAML flow style.
Each element in a `push-acks` array can have the following child keys:

| Key | Test Kind | Required | Value Type | Description |
| --- | --- | --- | --- | --- |
| publish | drive | no | array of [AckKind](#ackkind) | Queue of ACKs used sequentially to respond to MQTT PUBLISH messages. |
| subscribe | drive | no | array of [AckKind](#ackkind) | Queue of ACKs used sequentially to respond to MQTT SUBSCRIBE messages. |
| unsubscribe | drive | no | array of [AckKind](#ackkind) | Queue of ACKs used sequentially to respond to MQTT UNSUBSCRIBE messages. |

When a PUBLISH, SUBSCRIBE, or UNSUBSCRIBE is sent by the class under test, the test system attempts to dequeue an ACK from the appropriate queue.
If the queue is empty, the test system responds with a SUCCESS ACK; otherwise, the value from the head of the queue is used.

The value type for the elements in each queue is defined in the next subsection.

#### AckKind

The ACK kind is an enumeration that includes the following enumerated values:

| Value | Description |
| --- | --- |
| success | Acknowledge publication, subscription, or unsubscription with success code. |
| fail | Acknowledge publication, subscription, or unsubscription with unspecified error code. |
| drop | Do not acknowledge publication, subscription, or unsubscription. |

#### Catch

The value of `catch` defines an error that is expected to be caught, as in the following example:

```yaml
  catch:
    error-kind: invocation error
    in-application: !!bool true
    is-shallow: !!bool false
    is-remote: !!bool true
    status-code: 422
    message: "This is a content error with details"
    supplemental:
      property-name: 'requestheader'
      property-value: "requestValue"
```

The catch can have the following child keys:

| Key | Test Kind | Required | Value Type | Description |
| --- | --- | --- | --- | --- |
| error-kind | check | yes | string | The kind of error expected to be caught. |
| in-application | check | no | boolean | Whether the error occurs in user-supplied code rather than the SDK or its dependent components. |
| is-shallow | check | no | boolean | Whether the error is identified immediately after the API was called, prior to any attempted network communication. |
| is-remote | check | no | boolean | Whether the error is detected by a remote component. |
| status-code | check | no | integer or null | An HTTP status code from a remote service that initally caught the error. |
| message | check | no | string | The error message; should be checked only when explicitly set in a test case. |
| supplemental | check | no | map from string to string | Additional properties that may be set for some error kinds. |

See the [error model document](../../reference/error-model.md) for further details, including the supplemental properties that can be set in an error.

### Common miscellaneous items

#### Duration

A Duration defines a span of time, as in the following example:

```yaml
  message-expiry: { seconds: 20 }
```

By convention, this object is written in YAML flow style.
The duration can have the following child keys:

| Key | Required | Value Type | Description |
| --- | --- | --- | --- |
| hours | no | integer | The hours component of the duration. |
| minutes | no | integer | The minutes component of the duration. |
| seconds | no | integer | The seconds component of the duration. |
| milliseconds | no | integer | The milliseconds component of the duration. |
| microseconds | no | integer | The microseconds component of the duration. |

