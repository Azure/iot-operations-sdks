# Error model

This document proposes a model for reporting errors detected by the "Protocol" package/library in the SDKs.

## Purpose

The protocol API returns an error or throws an exception whenever it is unable to proceed with the operation it has been instructed to perform.
Since the circumstances that can prevent successful progress are numerous and varied, the errors represent a wide range of conditions that must be expressed to the calling application.
There are several relevant axes potentially usable for error categorization:

* Who: issue observed by application vs. infrastructure
* Where: problem observed locally vs. remotely
* What: underlying error or HTTP status code
* How: API issue vs. protocol issue

The intent of this proposal is to define an error model that efficiently conveys the most relevant information to the application without excessively burdening the application developer.
To this end, the following section presents required and desired characteristics for the proposed model.

## Requirements and desiderata

Following are requirements **(MUSTs)** and desiderata **(SHOULDs)** for the error model in this proposal.

1. The error model MUST consistently express the same information across programming languages.

    * This prohibits general use of standard error types, since these vary considerably from language to language.

1. The error model MUST distinguish between errors in application (user) code versus errors within the SDK library.

1. Error kinds MUST NOT be excessively fine-grained but SHOULD be sufficiently granular to convey topical information.

    * Opinions may differ on whether the subdivision of any particular condition is excessively granular, but any such subdivision needs at least be a good argument for how different actions by application code (or by a human operator) would justify the increased granularity.
    For example, the action to remediate a missing property might not be significantly differerent from the action to remediate an invalid property; however, a missing required property points to a lack of user knowledge, whereas the presence of an invalid property implies that the user knows the property exists.
    This distinction might justify subdividing these two conditions into separate error kinds.

1. Categorical distinctions among error kinds MUST NOT be excessive but SHOULD provide categories that indicate distinct forms of remediation.

1. The error kinds SHOULD express relevant and useful information to the application.

1. Error kinds SHOULD NOT be driven by implementation considerations such as which subcomponent raised an error.

    * Negative example 1: The C# SDK currently throws HybridLogicalClockException to report four different conditions: duplicate node ID, integer overflow, malformed HLC, and excessive clock drift.
    The fact that these errors are detected by the HybridLogicalClock component does not assist the application in appropriately responding to the error condition.
    From the application's perspective, the relevant fact is that the first two error conditions are internal logic errors, the third is a protocol error, and the last indicates an invalid state.

    * Negative example 2: The SDK uses HTTP status codes in response message headers to communicate error information from the command executor back to the command invoker.
    This is an implementation choice that should not dictate the error hierarchy exposed to users of the invoker.
    HTTP status codes should only be conveyed as supplementary information in errors.

1. Similar errors SHOULD be reported in similar ways.

    * Negative example 1: A message specifies a content type that is not supported by the implementation.
        * If message is command *request*, the C# SDK currently throws HttpRequestException with HttpStatusCode of UnsupportedMediaType.
        * If message is command *response*, the C# SDK currently throws NotSupportedException.

    * Negative example 2: A message payload cannot be deserialized.
        * If message is command *request*, the C# SDK currently throws HttpRequestException with HttpStatusCode of BadRequest.
        * If message is command *response*, the C# SDK currently throws SerializationException.

1. The SDK MUST translate any errors from underlying components into errors that are semantically appropriate for the SDK.

    * Example: If a "key not found" error is returned by a dictionary, the SDK should not surface this error to the application.
    Instead, the SDK should return an error that indicates the relevance of the missing key to the SDK.
    This might be "invalid configuration" if a config value should be present in the dictionary.
    Or it might be "internal logic error" if the algorithm was supposed to ensure that the key is present.

## Language-independent error hierarchy

In this proposal, the protocol APIs return/raise an error or errors derived from the language-specific base error implementation (where applicable).
This error or errors should indicate the specific *kind* of error that occurred, whether by type, field, enumeration, or other idiomatic construct appropriate to the language.
It should also represent and express the axes of categorization itemized at the top of this document (using English phrases instead of formal type names because the specific implementation and naming may vary by language).

The information being communicated does not have to literally be fields in a struct as in some languages it may make more sense to encode the information in the error design itself, e.g. an enumerated error with two variants "local" and "remote" may be preferable to a single error structure with a boolean field. Similarly, non-applicable fields may or may not be included depending on the error model, e.g. if using a specific error struct for Telemetry, there may be no need for an HTTP status field.

No matter how the error/errors are represented in a language, all relevant information MUST be included.

| Field | Type | Description | Axis | Required |
| --- | --- | --- | --- | --- |
| error kind | enumeration | the specific kind of error that occurred | | yes |
| in application | boolean | true if the error occurred in user-supplied code rather than the SDK or its dependent components | who | yes |
| is shallow | boolean | true if the error was identified immediately after the API was called, prior to any attempted network communication | how | yes |
| is remote | boolean | true if the error was detected by a remote component | where | yes |
| nested error | language-specific base error representation | an error from a dependent component that caused the Akri.Mqtt error being reported | what | no |
| HTTP status code | integer | an HTTP status code received from a remote service that caused the mRPC error being reported | what | no |

Additional fields provide supplementary information about the error condition.
These fields are all optional:

| Field | Type | Description |
| --- | --- | --- |
| header name | string | the name of an MQTT header that is missing or has an invalid value |
| header value | string | the value of an MQTT header that is invalid |
| timeout name | string | the name of a timeout condition that elapsed |
| timeout value | duration | the duration of a timeout condition that elapsed |
| property name | string | the name of a method argument or a property in a class, configuration file, or environment variable that is missing or has an invalid value |
| property value | variant | the value of a method argument or a property in a class, configuration file, or environment variable that is invalid |
| command name | string | the name of a command relevant to the mRPC error being reported |
| protocol version | string | The protocol version of the command request or response that was not supported. | 
| supported protocol major versions | int[] | The major protocol versions that are acceptable to the command executor if the executor rejected the command request or the major protocol versions that are acceptable to the command invoker if the invoker rejected the command response. |

The following table defines the proposed error kinds, value constraints or expected presence for each of the fixed fields in the first table above, and any additional fields that are used from the second table above.
Because the 'command name' field can potentially apply to any error, it is not listed specifically in the table.

| Error Kind | Description | In Application | Is Shallow | Is Remote | Nested Error | HTTP Status Code | Additional Fields Used |
| --- | --- | --- | --- | --- | --- | --- | --- |
| missing header | A required MQTT header property is missing from a received message. | false | false | either | no | maybe | header name |
| invalid header | An MQTT header property is has an invalid value in a received message. | false | false | either | no | maybe | header name, header value |
| invalid payload | MQTT payload cannot be serialized/deserialized. | false | either | either | maybe | maybe | |
| timeout | An operation was aborted due to timeout. | false | false | either | maybe | maybe | timeout name, timeout value |
| cancellation | An operation was canceled. | false | false | either | maybe | maybe | |
| invalid configuration | A class property, configuration file, or environment variable has an invalid value. | false | true | false | maybe | no | property name, property value |
| invalid argument | A method was called with an invalid argument value. | false | true | false | no | no | property name, property value |
| invalid state | The current program state is invalid vis-a-vis the method that was called. | false | either | either | no | no | property name, property value? |
| internal logic error | The client or service observed a condition that was thought to be impossible. | false | either | either | maybe | maybe | property name, property value? |
| unknown error | The client or service received an unexpected error from a dependent component. | false | either | either | yes | maybe | |
| execution error | The command processor encountered an error while executing the command. | true | false | true | no | yes | property name?, property value? |
| mqtt error | The MQTT communication encountered an error and failed. | false | false | false | maybe | no | |
| unsupported request version | The command executor that received the request doesn't support the provided protocol version. | false | false | true | no | yes | request protocol version, supported request protocol major versions |
| unsupported response version | The command invoker received a response that specifies a protocol version that the invoker does not support. | false | false | false | no | yes | response protocol version, supported response protocol major versions |

> Note: The Akri.Mqtt libraries in all languages are expected to be consistent in their use of additional fields, with two exceptions:
>
> * The 'property name' field should indicate the actual programming-language-specific name of the argument, parameter, or property that is missing or invalid.
> Since language conventions dictate casing rules, these values are expected to diverge across libraries, but only insofar as necessary to represent names in camelCase, PascalCase, or snake_case as appropriate.

To illustrate the use of these error kinds, [Appendix 1](#appendix-1-error-conditions-in-the-c-sdk) tabulates the error conditions currently recognized by the C# SDK and indicates which error kind should be used to express the condition.
This table also indicates the C# exception type currently thrown for each error condition.

## Language-specific interpretations of error model

The error model proposed above is intended to be agnostic to programming language.
Each language will realize this model in a manner that is appropriate for the features and conventions specific to that language.

### C\#

C# canonically reports errors by throwing exceptions.
The language provides a set of standard exception types, and many common libraries provide additions sets of exception types.
Custom exception types can be defined by inheriting from the base type `Exception`, which is the same base type for all standard and library exception types.

C# casing conventions specify that type names are PascalCase, property names are PascalCase, and enum values are also PascalCase.

The error kind enumeration is defined as follows:

```csharp
public enum AkriMqttErrorKind
{
    HeaderMissing,
    HeaderInvalid,
    PayloadInvalid,
    Timeout,
    Cancellation,
    ConfigurationInvalid,
    ArgumentInvalid,
    StateInvalid,
    InternalLogicError,
    UnknownError,
    ExecutionException,
    MqttError,
    UnsupportedRequestVersion,
    UnsupportedResponseVersion,
}
```

The base `Exception` type has a number of properties, but the only one of relevance to this error model is InnerException, which is mapped from the "nested error" field defined above in the [Language-independent error hierarchy](#language-independent-error-hierarchy) section.
Since this property is provided in the base type, it is not included in the Akri.Mqtt error type, defined as follows:

```csharp
public partial class AkriMqttException : Exception
{
    public AkriMqttErrorKind Kind { get; init; }
    public bool InApplication { get; init; }
    public bool IsShallow { get; init; }
    public bool IsRemote { get; init; }
    // public Exception? InnerException { get; init; } -- "nested error", inherited from base
    public int? HttpStatusCode { get; init; }

    public string? HeaderName { get; init; }
    public string? HeaderValue { get; init; }
    public string? TimeoutName { get; init; }
    public TimeSpan? TimeoutValue { get; init; }
    public string? PropertyName { get; init; }
    public object? PropertyValue { get; init; }
    public string? CommandName { get; init; }
    public string? ProtocolVersion { get; init; }
    public int[]? SupportedMajorProtocolVersions { get; init; }
}
```

Implementation note:
Since many fields are determined directly by error kind, instance construction can be simplified by providing static methods that instantiate the error type with kind-appropriate values.
For example:

```csharp
public partial class AkriMqttException : Exception
{
    public AkriMqttException(string? message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    public static AkriMqttException GetHeaderMissingException(
        string headerName,
        int? httpStatusCode = null)
    {
        return new AkriMqttException((httpStatusCode != null ? "request" : "response") + $" missing MQTT user property {headerName}")
        {
            Kind = AkriMqttErrorKind.HeaderMissing,
            InApplication = false,
            IsShallow = false,
            IsRemote = httpStatusCode != null,
            HttpStatusCode = httpStatusCode,
            HeaderName = headerName,
        };
    }
}
```

### Java

Java canonically reports errors by throwing them.
An instance can be thrown as long as its type inherits (indirectly) from the base type `Throwable`.
Concrete error types generally do not inherit from `Throwable` directly but rather from one of three defined subtypes whose hierarchy is illustrated in this diagram:

    Throwable (checked)
      ├ Exception (checked)
      |   └ RuntimeException (unchecked)
      └ Error (unchecked)

In Java every `Throwable` is either *checked* or *unchecked* depending on which subtype it inherits from.
If the type inherits from `Exception` but not `RuntimeException`, it is checked.
If the type inherits from `RuntimeException` or `Error`, it is unchecked.

The significance of checked exceptions is that the Java compiler enforces that functions (and methods) must handle every type of exception thrown by a function (or method) that they call.
Handling means that either the function catches the exception type or the function declares in its signature that it might throw the exception type.

Regarding the decision of whether to make a new exception checked or unchecked, a highly upvoted [answer on stackoverflow](https://stackoverflow.com/questions/27578/when-to-choose-checked-and-unchecked-exceptions) says the following:

> **Checked Exceptions** should be used for **predictable**, but **unpreventable** errors that are **reasonable to recover from**.
>
> **Unchecked Exceptions** should be used for everything else.
>
> I'll break this down for you, because most people misunderstand what this means.
>
> 1. **Predictable but unpreventable**: The caller did everything within their power to validate the input parameters, but some condition outside their control has caused the operation to fail. For example, you try reading a file but someone deletes it between the time you check if it exists and the time the read operation begins. By declaring a checked exception, you are telling the caller to anticipate this failure.
>
> 2. **Reasonable to recover from**: There is no point telling callers to anticipate exceptions that they cannot recover from. If a user attempts to read from an non-existing file, the caller can prompt them for a new filename. On the other hand, if the method fails due to a programming bug (invalid method arguments or buggy method implementation) there is nothing the application can do to fix the problem in mid-execution. The best it can do is log the problem and wait for the developer to fix it at a later time.
>
>Unless the exception you are throwing meets **all** of the above conditions it should use an Unchecked Exception.

Following this recommendation, the current proposal uses an *unchecked exception* for the Akri.Mqtt error type.
Specifically, since Java `Error` is generally not used for custom types but only for core system errors (such as `StackOverflowError` and `OutOfMemoryError`) the Akri.Mqtt error type inherits from `RuntimeException`.

Furthermore, in accordance with [Requirement](#requirements-and-desiderata) 8 above, all Java checked exceptions must be caught by the SDK.
If the SDK is unable to thoughtfully deal with the exception, it must encapsulate the caught exception inside its own unchecked exception type.
The kind can be "unknown error" if no more appropriate kind is apparent.

Java casing conventions specify that type names are PascalCase, field names are camelCase, and enum values are SCREAMING_SNAKE_CASE.

The error kind enumeration is defined as follows:

```java
public enum AkriMqttErrorKind {
    HEADER_MISSING,
    HEADER_INVALID,
    PAYLOAD_INVALID,
    TIMEOUT,
    CANCELLATION,
    CONFIGURATION_INVALID,
    ARGUMENT_INVALID,
    STATE_INVALID,
    INTERNAL_LOGIC_ERROR,
    UNKNOWN_ERROR,
    EXECUTION_EXCEPTION,
    MQTT_ERROR,
    UNSUPPORTED_REQUEST_VERSION,
    UNSUPPORTED_RESPONSE_VERSION,
}
```

The Akri.Mqtt error type is defined as follows:

```java
public class AkriMqttException : RuntimeException {
    public final AkriMqttErrorKind kind;
    public final boolean inApplication;
    public final boolean isShallow;
    public final boolean isRemote;
    public final Throwable nestedError;
    public final int httpStatusCode; // 0 for no status code

    public final String headerName;
    public final String headerValue;
    public final String timeoutName;
    public final Duration timeoutValue;
    public final String propertyName;
    public final Object propertyValue;
    public final String commandName;
    public final String ProtocolVersion;
    public final int[] SupportedMajorProtocolVersions;
}
```

### Rust

The conventional way to report errors in Rust is via the return value of a function.
For any function that returns type `T` in the happy case, the function signature should declare a return type of `Result<T, E>`, where `E` is the type returned when there is an error.

Type `E` can literally be any type, including primitive types.
It can even be the same type as `T`, because the `Result<T, E>` definition explicitly distinguishes between the two variants.
In practice, it is expected for error types to implement the [`Error` trait](https://doc.rust-lang.org/std/error/trait.Error.html):

```rust
pub trait Error: Debug + Display {
    // Provided methods
    fn source(&self) -> Option<&(dyn Error + 'static)> { ... }
    fn description(&self) -> &str { ... }
    fn cause(&self) -> Option<&dyn Error> { ... }
    fn provide<'a>(&'a self, request: &mut Request<'a>) { ... }
}
```

Rust defines a small set of standard error types, but they are quite limited in scope.
In practice, it is commonplace for libraries and applications to define their own custom error types.

Rust casing conventions specify that struct names are PascalCase, field names are snake_case, and enum values are PascalCase.

The error kind enumeration is defined as follows:

```rust
pub enum AIOProtocolErrorKind {
    HeaderMissing,
    HeaderInvalid,
    PayloadInvalid,
    Timeout,
    Cancellation,
    ConfigurationInvalid,
    ArgumentInvalid,
    StateInvalid,
    InternalLogicError,
    UnknownError,
    ExecutionException,
    MqttError,
    UnsupportedRequestVersion,
    UnsupportedResponseVersion,
}
```

The AIO Protocol error type is defined as follows:

```rust
pub enum Value {
    Integer(i32),
    Float(f64),
    String(String),
    Boolean(bool),
}

pub struct AIOProtocolError {
    message: Option<String>,
    kind: AIOProtocolErrorKind,
    in_application: bool,
    is_shallow: bool,
    is_remote: bool,
    nested_error: Option<Box<dyn Error>>,
    http_status_code: Option<u16>,
    header_name: Option<String>,
    header_value: Option<String>,
    timeout_name: Option<String>,
    timeout_value: Option<Duration>,
    property_name: Option<String>,
    property_value: Option<Value>,
    command_name: Option<String>,
    request_protocol_version: Option<String>,
    protocol_version: Option<String>,
    supported_major_protocol_versions: Option<Vec>,
}
```

The `AIOProtocolError` struct must implement the `Error` trait, but the details are omitted from this document:

```rust
impl Error for AIOProtocolError {
    ...
}
```

### Go

In Go, structures are strongly typed, but interfaces are duck typed.
Thus, any type can be used as an error type as long as it satisfies the the `error` interface definition by providing an `Error()` method:

```go
type error interface {
    Error() string
}
```

Although it is commonplace for errors to be reported merely with strings, which can be converted into generic errors via `error.New()`, the preferred practice for libraries is to define custom error types.

Go casing conventions specify that public struct names are PascalCase, public field names are PascalCase, and public const names are also PascalCase.

The error kind enumeration is defined as follows:

```go
type AkriMqttErrorKind int

const {
    HeaderMissing AkriMqttErrorKind = iota
    HeaderInvalid
    PayloadInvalid
    Timeout
    Cancellation
    ConfigurationInvalid
    ArgumentInvalid
    StateInvalid
    InternalLogicError
    UnknownError
    ExecutionException
    MqttError
    UnsupportedRequestVersion
    UnsupportedResponseVersion
}
```

The Akri.Mqtt error type is defined as follows:

```go
type AkriMqttError struct {
    Kind AkriMqttErrorKind
    InApplication bool
    UsShallow bool
    IsRemote bool

    NestedError error
    HttpStatusCode int // 0 for no status code
    HeaderName string
    HeaderValue string
    TimeoutName string
    TimeoutValue time.Duration
    PropertyName string
    PropertyValue any
    CommandName string
    ProtocolVersion string
    SupportedMajorProtocolVersions []int
}
```

The `AkriMqttError` struct must provide an `Error()` method, but the detais are omitted from this document:

```go
func (err AkriMqttError) Error() string {
}
```

### Python

Python has a sizeable set of [built-in exceptions](https://docs.python.org/3/library/exceptions.html), but to maintain alignment with other languages, the proposal is to use a custom error type.
This can be defined by inheriting directly or indirectly from `BaseException`.
For gRPC, the proposal is to inherit from the `RuntimeError` subtype, which is a very common parent type for exceptions in Python.

Python casing conventions specify that type names are PascalCase, property names are snake_case, and enum values are SCREAMING_SNAKE_CASE.

The error kind enumeration is defined as follows:

```python
class AkriMqttErrorKind(Enum):
    HEADER_MISSING = 1
    HEADER_INVALID = 2
    PAYLOAD_INVALID = 3
    TIMEOUT = 4
    CANCELLATION = 5
    CONFIGURATION_INVALID = 6
    ARGUMENT_INVALID = 7
    STATE_INVALID = 8
    INTERNAL_LOGIC_ERROR = 9
    UNKNOWN_ERROR = 10
    EXECUTION_EXCEPTION = 12
    MQTT_ERROR = 13
    UNSUPPORTED_REQUEST_VERSION = 14
    UNSUPPORTED_RESPONSE_VERSION = 15
```

The Akri.Mqtt error type is defined as follows:

```python
class AkriMqttException(RuntimeError):
    @property
    def kind(self):
        return self._kind

    @property
    def in_application(self):
        return self._in_application

    @property
    def is_shallow(self):
        return self._is_shallow

    @property
    def is_remote(self):
        return self._is_remote

    @property
    def nested_error(self):
        return self._nested_error

    @property
    def http_status_code(self):
        return self._http_status_code

    @property
    def header_name(self):
        return self._header_name

    @property
    def header_value(self):
        return self._header_value

    @property
    def timeout_name(self):
        return self._timeout_name

    @property
    def timeout_value(self):
        return self._timeout_value

    @property
    def property_name(self):
        return self._property_name

    @property
    def property_value(self):
        return self._property_value

    @property
    def command_name(self):
        return self._command_name

    @property
    def protocol_version(self):
        return self.protocol_version

    @property
    def supported_major_protocol_versions(self):
        return self.supported_major_protocol_versions
```

## Appendices

### Appendix 1: Error conditions in the C# SDK

The following table maps from each currently recognized error condition to (a) the C# exception type currently thrown and (b) the proposed new error kind to be used for this condition:

| Error Condition | Current C# Exception | Proposed Error Kind |
| --- | --- | --- |
| mqtt client supplied is null | ArgumentNullException | invalid argument |
| serializer supplied is null | ArgumentNullException | invalid argument |
| invalid MQTT topic pattern | ArgumentException | invalid argument OR invalid configuration |
| invalid MQTT topic namespace | ArgumentException | invalid configuration |
| invalid value for DefaultCommandTimeout | ArgumentException | invalid configuration |
| invalid value for CacheableDuration | ArgumentException | invalid configuration |
| Invalid environment variable value | ArgumentException | invalid configuration |
| missing environment variable | ArgumentException | invalid configuration |
| expired X509 certificate | ArgumentException | invalid state |
| MQTT client not configured for MQTT v5 | PlatformNotSupportedException | invalid configuration |
| command timeout | TimeoutException | timeout |
| command canceled | OperationCanceledException | cancellation |
| CommandResponseCache used incorrectly | InvalidOperationException | internal logic error |
| logic error in CommandResponseCache | Exception | internal logic error |
| logic error in MessageAcknowledger | Exception | internal logic error |
| unknown exception caught | Exception | unknown error |
| invalid MQTT connection settings | FormatException | invalid configuration |
| error in TLS settings | SecurityException | invalid configuration |
| response message missing header property | MissingFieldException | missing header |
| response message property unparseable | ArgumentException | invalid header |
| unrecognized status code in response | ArgumentOutOfRangeException | invalid header |
| unparseable status code in response | ArgumentException | invalid header |
| request payload expected but missing | HttpRequestException w/ BadRequest | invalid payload |
| response payload expected but missing | HttpRequestException w/ NoContent | invalid payload |
| request payload cannot be deserialized | HttpRequestException w/ BadRequest | invalid payload |
| response payload cannot be deserialized | SerializationException | invalid payload |
| request specifies unsupported content type | HttpRequestException w/ UnsupportedMediaType | invalid header |
| response specifies unsupported content type | NotSupportedException | invalid header |
| app-level error during command execution | HttpRequestException w/ InternalServerError | execution error |
| HLC duplicate node ID | HybridLogicalClockException | internal logic error |
| HLC integer overflow | HybridLogicalClockException | internal logic error |
| malformed HLC | HybridLogicalClockException | invalid header |
| HLC excessive clock drift | HybridLogicalClockException | invalid state |

### Appendix 2: HTTP status codes in command response messages

The SDK uses HTTP status codes to communicate error information from the command executor back to the command invoker.
The following table lists the HTTP status codes, conditions on other fields in the response message, and the mapping to error kinds.

| HTTP Status | HTTP Mnemonic | IsApplicationError | InvalidPropertyName header present | InvalidPropertyValue header present | Error Kind |
| --- | --- | --- | --- | --- | --- |
| 200 | OK | | | | (none) |
| 204 | No Content | | | | (none) |
| 400 | Bad Request | false | yes | yes | invalid header |
| 400 | Bad Request | false | yes | no | missing header |
| 400 | Bad Request | false | no | | invalid payload |
| 408 | Request Timeout | false | yes | yes | timeout |
| 415 | Unsupported Media Type | false | yes | yes | invalid header |
| 500 | Internal Server Error | false | no | | unknown error |
| 500 | Internal Server Error | false | yes | | internal logic error |
| 500 | Internal Server Error | true | maybe | | execution error |
| 503 | Service Unavailable | false | maybe | maybe | invalid state |
| 505 | Version Not Supported | false | yes | maybe | version not supported |
