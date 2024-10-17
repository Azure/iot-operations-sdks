# Error Conditions

This document characterizes the error conditions that can be detected by the `CommandExecutor` or the `CommandInvoker`, and it describes the appropriate way that these errors should be communicated to users of the libraries.

## Invalid Request Messages

When the `CommandExecutor` determines that a request message is invalid, it usually responds with a response message containing an HTTP status code that characterizes the error.
(This contrasts with a status code of 200 OK or 204 No Content when there is no error.)
It may also set additional metadata properties in the response to provide further information about the error condition.

There are cases in which the nature of a problem in a request message prevents the `CommandExecutor` from publishing a response.
For example, if the request contains no `ResponseTopic` property, the executor does not know the topic to which the error response should be published.
In these cases, the `CommandExecutor` should log the error, acknowledge receipt of the request (to satisfy the MQTT protocol), but not publish a response.
These conditions will eventually lead to a timeout in the `CommandInvoker`.

When the `CommandInvoker` receives a response message that contains a status code indicating an error, it responds to the command invocation by surfacing an [Akri.Mqtt error](./error-model.md).
The 'is remote' field of this error will be true.

The following table illustrates, for each possible error condition in a request message, what the `CommandExecutor` should respond via the Status, InvalidPropertyName, and InvalidPropertyValue fields.
(For all conditions in this table, IsApplicationError is omitted or false.)
The table also illustrates which Akri.Mqtt error kind the `CommandInvoker` should map this respose to.
If a request message has multiple error conditions, the `CommandExecutor` is free to select from among the appropriate responses.
For comprehensiveness, the table includes rows illustrating conditions that are not considered to be errors; these requests should be processed as normal.

| Condition | Error? | Status | InvalidPropertyName | InvalidPropertyValue | Error Kind |
| --- | --- | --- | --- | --- | --- |
| `ContentType` missing | no | | | | |
| `ContentType`not recognized | yes | 415 | "Content Type" | (property value) | invalid header |
| `ContentType`not supported | yes | 415 | "Content Type" | (property value) | invalid header |
| `FormatIndicator` invalid (not 0 or 1) | yes | 415 | "Payload Format Indicator" | (property value) | invalid header |
| `FormatIndicator` wrong for `ContentType` | yes | 415 | "Payload Format Indicator" | (property value) | invalid header |
| `CorrelationData` missing | yes | 400 | "Correlation Data" | (none) | invalid header |
| `CorrelationData` not GUID string | yes | 400 | "Correlation Data" | (property value) | invalid header |
| `ResponseTopic` missing | yes | (no response) | | | timeout |
| `ResponseTopic` invalid | yes | (no response) | | | timeout |
| `MessageExpiry` missing | yes | 400 | "Message Expiry" | (none) | invalid header |
| `Timestamp` missing | no | | | | |
| `Timestamp` invalid | yes | 400 | "__ts" |  (property value) | invalid header |
| `FencingToken` missing | no | | | | |
| `FencingToken` invalid | yes | 400 | "__ft" |  (property value) | invalid header |
| `InvokerClientId` missing | yes | 400 | "__invId" | (none) | missing header |
| payload present when not expected | yes | 400 | (none) | | invalid payload |
| payload absent when expected | yes | 400 | (none) | | invalid payload |
| payload unable to deserialize | yes | 400 | (none) | | invalid payload |

## CommandExecutor Command Errors

When the `CommandExecutor` encounters an error condition while processing a Command request, it responds with a response message containing an HTTP status code that characterizes the error.
It may also set additional metadata properties in the response to provide further information about the error condition.

There are cases in which the nature of a problem prevents the `CommandExecutor` from publishing a response.
For example, if the message expiry interval is exceeded during processing, any response would have no remaining time to return to the `CommandInvoker`, and there is no value in publishing a response that will not be received.
In these cases, the `CommandExecutor` should log the error, acknowledge receipt of the request (to satisfy the MQTT protocol), but not publish a response.
These conditions will eventually lead to a timeout in the `CommandInvoker`.

When the `CommandInvoker` receives a response message that contains a status code indicating an error, it responds to the command invocation with an [Akri.Mqtt error](./error-model.md).
The 'is remote' field of this error will be true.

The following table illustrates, for each error condition, what response should be sent by the `CommandExecutor` and which Akri.Mqtt error the `CommandInvoker` should map this respose to.
(For all conditions in this table, the InvalidPropertyName and InvalidPropertyValue fields are omitted.)

| Error Condition | Status | IsApplicationError | InvalidPropertyName | InvalidPropertyValue | Error Kind |
| --- | --- | --- | --- | --- | --- |
| message expiry exceeded | (no response) | | | | timeout |
| execution timeout exceeded | 408 | false | "ExecutionTimeout" | (timeout duration) | timeout |
| CommandResponseCache used incorrectly | 500 | false | "CorrelationData" | (property value) | internal logic error |
| unknown error from dependent component | 500 | false | (none) | | unknown error |
| app-level error in request | 422 | true | (set by user code) | (set by user code) | invocation error |
| app-level error during command execution | 500 | true | (none) | | execution error |
| HLC integer overflow | 500 | false | "Counter" | (none) | internal logic error |
| HLC excessive clock drift | 503 | false | "MaxClockDrift" | (none) | invalid state |

> Note: When an unknown error originates from a dependent component, it is recommended to include any nested error text in the status message to assist a human user in diagnosing the error condition.

## CommandExecutor Non-Command Errors

Prior to the receipt of a Command request, the `CommandExecutor` may encounter an error condition while initializing or when one of its properties is set.
In this case, it surfaces an [Akri.Mqtt error](./error-model.md).
The 'is remote' field of this error will be false.

The following table illustrates, for each error condition, which Akri.Mqtt error the `CommandExecutor` should surface.

| Error Condition | Error Kind |
| --- | --- |
| command name is null or empty | invalid configuration |
| mqtt client is null | invalid configuration |
| serializer is null | invalid configuration |
| cacheable duration has negative value | invalid configuration |
| idemotent is false and cacheable duration is non-zero | invalid configuration |
| execution timeout is less than 1ms (including negative or zero) | invalid configuration |
| topic namespace invalid | invalid configuration |
| topic pattern is null or empty | invalid configuration |
| topic pattern is invalid | invalid configuration |
| topic pattern contains token with no valid replacement | invalid configuration |
| MQTT client not configured for MQTT v5 | invalid configuration |
| subscribe failure | mqtt error |

## Invalid Response Messages

When the `CommandInvoker` determines that a response message is invalid, it usually responds to the command invocation with an [Akri.Mqtt error](./error-model.md).
The 'is remote' field of this error will be false.

There are cases in which the nature of a problem in a response message prevents the `CommandInvoker` from responding to the invocation.
For example, if the response contains no `CorrelationData` property, the invoker does not know which invocation should receive the error.
In these cases, the `CommandInvoker` should log the error but not surface it to any invocation.

The following table illustrates, for each error condition in a response message, which Akri.Mqtt error the `CommandInvoker` should surface.
If a response message has multiple error conditions, the `CommandInvoker` is free to select from among the appropriate errors.
For comprehensivess, the table includes rows illustrating conditions that are not considered to be errors; these responses should be processed as normal.

Response messages that indicate remotely detected errors in request messages should be processed liberally.
Instead of checking for detailed consistency between status codes and which supplemental properties are included, the invoker should relay the remote error as best it can.
There is little benefit from checking property/code consistency, and avoiding such a check maintains opportunity for backward- and forward-compatibility if there is any iteration on an appropriate selection of supplemental information.

| Condition | Error? | Error Kind |
| --- | --- | --- |
| `ContentType` missing | no | |
| `ContentType`not recognized | yes | invalid header |
| `ContentType`not supported | yes | invalid header |
| `FormatIndicator` invalid (not 0 or 1) | yes | invalid header |
| `FormatIndicator` wrong for `ContentType` | yes | invalid header |
| `CorrelationData` matches no active invocation | yes | (no return) |
| `MessageExpiry` missing | no | |
| `Timestamp` missing | no | |
| `Timestamp` invalid | yes | invalid header |
| `Status` missing | yes | missing header |
| `Status` value not expected (per [table](./error-model.md/#appendix-2-http-status-codes-in-command-response-messages)) | yes | invalid header |
| `Status` = 204 and expected payload present | yes | invalid header |
| `StatusMessage` missing | no | |
| `IsApplicationError` missing | no | |
| `InvalidPropertyName` missing | no | |
| `InvalidPropertyValue` missing | no | |
| payload present when not expected | yes | invalid payload |
| payload absent when expected | yes | invalid payload |
| payload unable to deserialize | yes | invalid payload |

> Note: `IsApplicationError` is interpreted as false if the property is omitted, or has no value, or has a value that case-insensitively equals "false".
Otherwise, the property is interpreted as true.

## CommandInvoker Command Errors

When the `CommandInvoker` encounters an error condition while invoking a Command, it surfaces an [Akri.Mqtt error](./error-model.md).
The 'is remote' field of this error will be false.

The following table illustrates, for each error condition, which Akri.Mqtt error the `CommandInvoker` should surface.

| Error Condition | Error Kind |
| --- | --- |
| command timeout is less than 1ms (including negative or zero) or greater than u32 max | invalid configuration |
| topic pattern contains {executorId} token but no executor ID supplied | invalid configuration |
| command times out | timeout |
| command is canceled | cancellation |
| unknown error from dependent component | unknown error |
| HLC integer overflow | internal logic error |
| HLC excessive clock drift | invalid state |

## CommandInvoker Non-Command Errors

The `CommandInvoker` may encounter an error condition while initializing or when one of its properties is set, in which case it surfaces an [Akri.Mqtt error](./error-model.md).
The 'is remote' field of this error will be false.
The invoker may defer checking one or more of these conditions until a Command is invoked.

The following table illustrates, for each error condition, which Akri.Mqtt error the `CommandInvoker` should surface.

| Error Condition | Error Kind |
| --- | --- |
| command name is null or empty | invalid configuration |
| mqtt client is null | invalid configuration |
| serializer is null | invalid configuration |
| topic namespace invalid | invalid configuration |
| topic pattern is null or empty | invalid configuration |
| topic pattern is invalid | invalid configuration |
| topic pattern contains token with no valid replacement | invalid configuration |
| response topic prefix invalid | invalid configuration |
| response topic suffix invalid | invalid configuration |
| MQTT client not configured for MQTT v5 | invalid configuration |
| invalid MQTT connection settings | invalid configuration |
| subscribe failure | mqtt error |
