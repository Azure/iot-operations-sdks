# ADR 19: Modeling User Errors

## Context

[ADR 15][1] removed from the SDKs any mechanism for conveying user-level errors.
Yet we know that our users have a desire to communicate user-level errors, and they would like to employ language-appropriate mechanism for conveying these errors.
We have even been asked for recommendations and samples for modeling user errors in DTDL.

### Constraints and Assumptions

The specific proposal herein is predicated on the following understanding:

* Constraint: Releasing a new version of the DTDL language is **not** practicable in the desired timeframe.
* Assumption: Releasing a new version of the DTDL Mqtt extension **is** practicable in the desired timeframe.

## Decision

This ADR defines a modeling approach for defining user-level errors.
The approach requires adding several new adjunct types to the DTDL Mqtt extension, which will bump the extension version from 2 to 3.

This ADR also presents a code-generation mechanism for generating language-appropriate code that will express and convey user-level errors.
Implementing this mechanism will require changes to the ProtocolCompiler.

## Contents

This is a large-ish document for an ADR.
To facilitate review, following are links to key sections:

* A [table](#mqtt-extension-version-3) of new adjunct types proposed to be added to the MQTT extension
* A [sample model](#sample-model) defining a simple command for illustrative purposes in this document
* An [enhanced model](#enhanced-model) that uses the new adjunct types to add error response information to the sample model
* Using [Result, NormalResult, and ErrorResult](#result-normalresult-and-errorresult-adjunct-types) adjunct types to define schemas for normal and error results
* Using [Message and ErrorMessage](#message-and-errormessage-adjunct-types) adjunct types to define error information for a language-appropriate error type
* An illustration of [C# code generation and usage](#c-code-generation), including both [server](#c-server-side-code)- and [client](#c-client-side-code)-side code
* An illustration of [Go code generation and usage](#go-code-generation), including both [server](#go-server-side-code)- and [client](#go-client-side-code)-side code
* An illustration of [Rust code generation and usage](#rust-code-generation), including both [server](#rust-server-side-code)- and [client](#rust-client-side-code)-side code

## MQTT extension, version 3

To enable models to express error information in a way that can be understood by the ProtocolCompiler, the following new adjunct types are proposed for version 3 of the DTDL Mqtt extension.

| Adjunct Type | Material Cotype | Meaning |
| --- | --- | --- |
| `Result` | `Object` | Indicates that the cotyped `Object` defines the composite (normal and error) result type that is returned from the command execution function |
| `NormalResult` | `Field` | Indicates that the cotyped `Field` within a `Result/Object` defines the result returned under normal (non-error) conditions |
| `ErrorResult` | `Field` | Indicates that the cotyped `Field` within a `Result/Object` defines the result returned under error conditions |
| `Error` | `Object` | Indicates that the cotyped `Object` defines a language-appropriate error type |
| `ErrorMessage` | `Field` | Indicates that the cotyped string `Field` within an `Error/Object` defines an error message that should be conveyed via language-appropriate means |

Use of these new types is illustrated [below](#enhanced-model).

## Sample model

The following DTDL model defines an "increment" command with a response schema that is an integer value named "counterValue".
The model does not express any error information that can be returned in lieu of the "counterValue" response.

```json
{
  "@context": [ "dtmi:dtdl:context;4", "dtmi:dtdl:extension:mqtt;2" ],
  "@id": "dtmi:com:example:CounterCollection;1",
  "@type": [ "Interface", "Mqtt" ],
  "commandTopic": "rpc/command-samples/{executorId}/{commandName}",
  "payloadFormat": "Json/ecma/404",
  "contents": [
    {
      "@type": "Command",
      "name": "increment",
      "request": {
        "name": "counterName",
        "schema": "string"
      },
      "response": {
        "name": "counterValue",
        "schema": "integer"
      }
    }
  ]
}
```

## Enhanced model

The following DTDL model enhances the above model with error response information that is cotyped with the proposed new [adjunct types](#mqtt-extension-version-3).

```json
{
  "@context": [ "dtmi:dtdl:context;4", "dtmi:dtdl:extension:mqtt;3" ],
  "@id": "dtmi:com:example:CounterCollection;1",
  "@type": [ "Interface", "Mqtt" ],
  "commandTopic": "rpc/command-samples/{executorId}/{commandName}",
  "payloadFormat": "Json/ecma/404",
  "contents": [
    {
      "@type": "Command",
      "name": "increment",
      "request": {
        "name": "counterName",
        "schema": "string"
      },
      "response": {
        "name": "incrementResponse",
        "schema": {
          "@type": [ "Object", "Result" ],
          "fields": [
            {
              "@type": [ "Field", "NormalResult" ],
              "name": "counterValue",
              "schema": "integer"
            },
            {
              "@type": [ "Field", "ErrorResult" ],
              "name": "incrementError",
              "schema": "dtmi:com:example:CounterCollection:CounterError;1"
            }
          ]
        }
      }
    }
  ],
  "schemas": [
    {
      "@id": "dtmi:com:example:CounterCollection:CounterError;1",
      "@type": [ "Object", "Error" ],
      "fields": [
        {
          "@type": [ "Field", "ErrorMessage" ],
          "name": "explanation",
          "schema": "string"
        },
        {
          "name": "condition",
          "schema": {
            "@type": "Enum",
            "valueSchema": "integer",
            "enumValues": [
              {
                "name": "counterNotFound",
                "enumValue": 1
              },
              {
                "name": "counterOverflow",
                "enumValue": 2
              }
            ]
          }
        }
      ]
    }
  ]
}
```

This is a lot for a reader to digest at once.
To assist in understanding, we will describe the important aspects of it piecemeal.

### Result, NormalResult, and ErrorResult adjunct types

First off, note that the model's "response" property has expanded from this:

```json
"response": {
  "name": "counterValue",
  "schema": "integer"
}
```

to this:

```json
"response": {
"name": "incrementResponse",
"schema": {
    "@type": [ "Object", "Result" ],
    "fields": [
      {
        "@type": [ "Field", "NormalResult" ],
        "name": "counterValue",
        "schema": "integer"
      },
      {
        "@type": [ "Field", "ErrorResult" ],
        "name": "incrementError",
        "schema": "dtmi:com:example:CounterCollection:CounterError;1"
      }
    ]
  }
}
```

The original information (`"name": "counterValue", "schema": "integer"`) is still present, but it is now in a `Field` cotyped `NormalResult`, which is nested inside an `Object` that is cotyped `Result`.
In words, the Command response is no longer merely an integer named "counterValue".
It is now a composite result with _normal_ and _error_ components.

The DTDL definition for the response above generates a JSON Schema definition for the response as follows:

```json
{
  "$schema": "https://json-schema.org/draft-07/schema",
  "title": "IncrementResponseSchema",
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "counterValue": {
      "type": "integer", "minimum": -2147483648, "maximum": 2147483647
    },
    "incrementError": {
      "$ref": "CounterError.schema.json"
    }
  }
}
```

The above schema, `IncrementResponseSchema`, defines the representation of the response on the wire.
The JSON object has two fields, both of which are optional (although in practice one must have a value):

* a `counterValue` field that conveys the incremented counter value under normal circumstances
* an `incrementError` field that conveys error information under exceptional circumstances

The above schema is not seen by user-level code on either the client or server side.
User code sees the same response schema that was generated for the un-enhanced model, which has a single required `counterValue` field:

```json
{
  "$schema": "https://json-schema.org/draft-07/schema",
  "title": "IncrementResponsePayload",
  "type": "object",
  "additionalProperties": false,
  "required": [ "counterValue" ],
  "properties": {
    "counterValue": {
      "type": "integer", "minimum": -2147483648, "maximum": 2147483647
    }
  }
}
```

This schema, `IncrementResponsePayload`, defines the representation of the normal response from user service code to user client code, as illustrated below in the code-generation sections.

When an error is returned instead of the normal response, the specific error information is defined in the model by the `Field` cotyped `ErrorResult`.
The schema for this error information is defined elsewhere in the model for cleanliness, and it will be described in the next section.

### Message and ErrorMessage adjunct types

The choice of what information to include in an error result is entirely up to the user.
In the enhanced model above, the schema for information in the `ErrorResult` is defined as follows:

```json
    {
      "@id": "dtmi:com:example:CounterCollection:CounterError;1",
      "@type": [ "Object", "Error" ],
      "fields": [
        {
          "@type": [ "Field", "ErrorMessage" ],
          "name": "explanation",
          "schema": "string"
        },
        {
          "name": "condition",
          "schema": {
            "@type": "Enum",
            "valueSchema": "integer",
            "enumValues": [
              {
                "name": "counterNotFound",
                "enumValue": 1
              },
              {
                "name": "counterOverflow",
                "enumValue": 2
              }
            ]
          }
        }
      ]
    }
```

Only two aspects of this definition employ the proposed adjunct types to affect code generation.
The first aspect is that the `Object` is cotyped `Error`, which indicates that the information in the `Object` should be encapsulated in a language-appropriate error type.
The second aspect is that the string field named "explanation" is cotyped `ErrorMessage`, which indicates that the string value is an error message that should be conveyed via language-appropriate means.

Any other information in the error result is entirely at the discretion of the user.
In this example, there is an additional field named "condition", which is an `Enum` indicating the error condition that occurred.

## C# code generation

The DTDL `Object` with ID `dtmi:com:example:CounterCollection:CounterError;1` will generate a C# object named `CounterError`, exactly as before:

```csharp
public partial class CounterError
{
    [JsonPropertyName("condition")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ConditionSchema? Condition { get; set; } = default;

    [JsonPropertyName("explanation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Explanation { get; set; } = default;

}
```

In addition, because the DTDL `Object` has a cotype of `Error`, a `CounterErrorException` will be generated to wrap the `CounterError` class into a custom exception type:

```csharp
public partial class CounterErrorException : Exception
{
    public CounterErrorException(CounterError counterError)
        : base(counterError.Explanation)
    {
        CounterError = counterError;
    }

    public CounterError CounterError { get; }
}
```

Note that the constructor calls the base constructor with an exception message that is the value of the `CounterError` `Explanation` property.
This code is generated because the "explanation" field in the DTDL `Object` is cotyped `ErrorMessage`.

The [sample model without error info](#sample-model) generates the following abstract method signature for the user's server code to override:

```csharp
public abstract Task<ExtendedResponse<IncrementResponsePayload>> IncrementAsync(
    IncrementRequestPayload request,
    CommandRequestMetadata requestMetadata,
    CancellationToken cancellationToken);
```

And it produces a client-side invocation method with this signature:

```csharp
public RpcCallAsync<IncrementResponsePayload> IncrementAsync(
    IncrementRequestPayload request,
    CommandRequestMetadata? requestMetadata = null,
    IReadOnlyDictionary<string, string>? transientTopicTokenMap = null,
    TimeSpan? commandTimeout = default,
    CancellationToken cancellationToken = default)
```

These signatures remain unchanged when error info is added by replacing the [original model](#sample-model) with the [enhanced model](#enhanced-model).
What changes is that the server-side generated code becomes able to catch a `CounterErrorException` thrown by user code, and the client-side generated code becomes able to throw a `CounterErrorException` to be caught by user code.

### C# Server-side code

Here is an example server-side user-code command execution function, which throws a `CounterErrorException` when it encounters a problem:

```csharp
public override Task<ExtendedResponse<IncrementResponsePayload>> IncrementAsync(
    IncrementRequestPayload request,
    CommandRequestMetadata requestMetadata,
    CancellationToken cancellationToken)
{
    if (!counterValues.TryGetValue(request.CounterName, out int currentValue))
    {
        throw new CounterErrorException(new CounterError
        {
            Condition = ConditionSchema.CounterNotFound,
            Explanation = $"Counter {request.CounterName} not found in counter collection",
        });
    }

    if (currentValue == int.MaxValue)
    {
        throw new CounterErrorException(new CounterError
        {
            Condition = ConditionSchema.CounterOverflow,
            Explanation = $"Counter {request.CounterName} has saturated; no further increment is possible",
        });
    }

    int newValue = currentValue + 1;
    counterValues[request.CounterName] = newValue;

    return Task.FromResult(ExtendedResponse<IncrementResponsePayload>.CreateFromResponse(
        new IncrementResponsePayload { CounterValue = newValue }));
}
```

When the user code throws a `CounterErrorException`, it is caught by server-side generated code that maps the error information into a generated C# class that is serialized and returned to the client:

```csharp
try
{
    ExtendedResponse<IncrementResponsePayload> extResp =
        await this.IncrementAsync(req.Request!, req.RequestMetadata!, cancellationToken);

    return new ExtendedResponse<IncrementResponseSchema>
    {
        Response = new IncrementResponseSchema { CounterValue = extResp.Response.CounterValue },
        ResponseMetadata = extResp.ResponseMetadata,
    };
}
catch (CounterErrorException ceEx)
{
    return ExtendedResponse<IncrementResponseSchema>.CreateFromResponse(
        new IncrementResponseSchema { IncrementError = ceEx.CounterError });
}
```

### C# Client-side code

Generated client-side code deserializes the received error information into a new `CounterErrorException` which it then throws:

```csharp
ExtendedResponse<IncrementResponseSchema> extResp =
    await this.incrementCommandInvoker.InvokeCommandAsync(
    request, requestMetadata, transientTopicTokenMap, commandTimeout, cancellationToken);

if (extResp.Response.IncrementError != null)
{
    throw new CounterErrorException(extResp.Response.IncrementError);
}
else if (extResp.Response.CounterValue != null)
{
    return new ExtendedResponse<IncrementResponsePayload>
    {
        Response = new IncrementResponsePayload { CounterValue = (int)extResp.Response.CounterValue },
    };
}
else
{
    throw new AkriMqttException("Command response has neither normal nor error payload content")
    {
        Kind = AkriMqttErrorKind.PayloadInvalid,
        InApplication = true,
        IsShallow = false,
        IsRemote = false,
    };
}
```

Here is example client-side code that invokes the command and is prepared for a `CounterErrorException` to be thrown:

```csharp
try
{
    IncrementResponsePayload response = await counterCollectionClient.IncrementAsync(
        new IncrementRequestPayload { CounterName = counterName });

    Console.WriteLine($"{response.CounterValue}");
}
catch (CounterErrorException counterException)
{
    Console.WriteLine($"The increment failed with exception: {counterException.Message}");

    switch (counterException.CounterError.Condition)
    {
        case ConditionSchema.CounterNotFound:
            Console.WriteLine($"Counter {counterName} was not found");
            break;
        case ConditionSchema.CounterOverflow:
            Console.WriteLine($"Counter {counterName} has overflowed");
            break;
    }
}
```

Note that this code reads the standard `Message` property of the exception.
An alternative way to access this same value is via the `Explanation` property of the nested `CounterError`, but the example above illustrates that the standard C# mechanism for conveying error strings is usable.

## Go code generation

The DTDL `Object` with ID `dtmi:com:example:CounterCollection:CounterError;1` will generate a Go struct named `CounterError`, exactly as before:

```go
type CounterError struct {
    Condition   *ConditionSchema `json:"condition,omitempty"`
    Explanation *string          `json:"explanation,omitempty"`
}
```

In addition, because the DTDL `Object` has a cotype of `Error`, an `Error` receiver will be generated so that the `CounterError` duck-types to the `error` interface:

```go
func (e *CounterError) Error() string {
    return *e.Explanation
}
```

Note that the return value for the `Error` receiver is the value of the `CounterError` `Explanation` property.
This code is generated because the "explanation" field in the DTDL `Object` is cotyped `ErrorMessage`.

The [sample model without error info](#sample-model) generates the following function type for the user-code command handler:

```go
protocol.CommandHandler[IncrementRequestPayload, IncrementResponsePayload]
```

This expands to:

```go
func(
    context.Context,
    *protocol.CommandRequest[countercollection.IncrementRequestPayload],
) (*protocol.CommandResponse[countercollection.IncrementResponsePayload], error)
```

And it produces a client-side invocation method with this signature:

```go
func (invoker IncrementCommandInvoker) Increment(
    ctx context.Context,
    request IncrementRequestPayload,
    opt ...protocol.InvokeOption,
) (*protocol.CommandResponse[IncrementResponsePayload], error)
```

These signatures remain unchanged when error info is added by replacing the [original model](#sample-model) with the [enhanced model](#enhanced-model).
What changes is that the server-side generated code becomes able to recognize an error return from user code that is a `CounterError`, and the client-side generated code becomes able to return a `CounterError` as its error return to user code.

### Go Server-side code

Here is an example server-side user-code command handler, which returns an error that is a `CounterError` when it encounters a problem:

```go
func (h *Handlers) Increment(
    ctx context.Context,
    req *protocol.CommandRequest[countercollection.IncrementRequestPayload],
) (*protocol.CommandResponse[countercollection.IncrementResponsePayload], error) {
    currentValue := h.counterValues[req.Payload.CounterName]
    if currentValue == 0 {
        condition := countercollection.CounterNotFound
        explanation := fmt.Sprintf("Counter %s not found in counter collection", req.Payload.CounterName)
        return nil, &countercollection.CounterError{
            Condition: &condition,
            Explanation: &explanation,
        }
    }

    if currentValue == math.MaxInt32 {
        condition := countercollection.CounterOverflow
        explanation := fmt.Sprintf("Counter %s has saturated; no further increment is possible", req.Payload.CounterName)
        return nil, &countercollection.CounterError{
            Condition: &condition,
            Explanation: &explanation,
        }
    }

    newValue := currentValue + 1
    h.counterValues[req.Payload.CounterName] = newValue

    return protocol.Respond(countercollection.IncrementResponsePayload{
        CounterValue: newValue,
    })
}
```

When the user code returns an error, the server-side generated code checks whether it is a `CounterError`.
If it is, the generated code maps the error information into a generated C# class that is serialized and returned to the client:

```go
    response, err := incrementWrapper.handler(ctx, req)
    if err != nil {
        counterError, ok := err.(*CounterError)
        if !ok {
            return nil, err
        }

        return protocol.Respond(IncrementResponseSchema{
            CounterValue:   nil,
            IncrementError: counterError,
        })
    }

    mappedResponse := protocol.CommandResponse[IncrementResponseSchema]{
        protocol.Message[IncrementResponseSchema]{
            Payload: IncrementResponseSchema{
                CounterValue:   &response.Payload.CounterValue,
                IncrementError: nil,
            },
            ClientID:        response.ClientID,
            CorrelationData: response.CorrelationData,
            Timestamp:       response.Timestamp,
            TopicTokens:     response.TopicTokens,
            Metadata:        response.Metadata,
        },
    }

    return &mappedResponse, nil
```

### Go Client-side code

Generated client-side code checks whether the response received from the server is a `CounterError`.
If it is, the generated code returns the `CounterError` via the error return to user code.

```go
response, err := invoker.Invoke(
    ctx,
    request,
    &invokeOpts,
)

if err != nil {
    return nil, err
}

if response.Payload.IncrementError != nil {
    return nil, response.Payload.IncrementError
}

if response.Payload.CounterValue == nil {
    return nil, &errors.Client{
        Base: errors.Base{
            Message: "Command response has neither normal nor error payload content",
            Kind:    errors.PayloadInvalid,
        },
        IsShallow: false,
    }
}

mappedResponse := protocol.CommandResponse[IncrementResponsePayload]{
    protocol.Message[IncrementResponsePayload]{
        Payload: IncrementResponsePayload{
            CounterValue: *response.Payload.CounterValue,
        },
        ClientID:        response.ClientID,
        CorrelationData: response.CorrelationData,
        Timestamp:       response.Timestamp,
        TopicTokens:     response.TopicTokens,
        Metadata:        response.Metadata,
    },
}

return &mappedResponse, nil
```

Here is example client-side code that invokes the command and is prepared for an error return of `CounterError`:

```go
incResp, err := counterClient.Increment(ctx, countercollection.IncrementRequestPayload{
    CounterName: counterName,
})

if err == nil {
    fmt.Printf("result = %d", incResp.Payload.CounterValue)
} else {
    fmt.Printf("Increment returned error %s", err.Error())

    counterError, ok := err.(*countercollection.CounterError)
    if ok && counterError.Condition != nil {
        switch *counterError.Condition {
        case countercollection.CounterNotFound:
            fmt.Printf("Counter %s was not found", counterName)
        case countercollection.CounterOverflow:
            fmt.Printf("Counter %s has overflowed", counterName)
        }
    }
}
```

Note that this code calls the standard `Error` handler of the error.
An alternative way to access this same value is via the `Explanation` property, but the example above illustrates that the standard Go mechanism for conveying error strings is usable.

## Rust code generation

The DTDL `Object` with ID `dtmi:com:example:CounterCollection:CounterError;1` will generate a Rust struct named `CounterError`, exactly as before:

```rust
#[derive(Serialize, Deserialize, Debug, Clone, Builder)]
pub struct CounterError {
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub condition: Option<ConditionSchema>,

    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub explanation: Option<String>,
}
```

In addition, because the DTDL `Object` has a cotype of `Error`, implementations of the `Display` and `Error` traits will be generated so that the `CounterError` can be used as an error:

```rust
impl fmt::Display for CounterError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        if let Some(message) = &self.explanation {
            write!(f, "{message}")
        } else {
            write!(f, "CounterError")
        }
    }
}

impl Error for CounterError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        None
    }
}
```

Note that the `fmt` function displays the value of the `CounterError` `explanation` field.
This code is generated because the "explanation" field in the DTDL `Object` is cotyped `ErrorMessage`.

The [sample model without error info](#sample-model) generates the following signature for the `recv` method of the `IncrementCommandExecutor`:

```rust
pub async fn recv(&mut self) -> Option<Result<IncrementRequest, AIOProtocolError>> 
```

And it produces a client-side invocation method with this signature:

```rust
pub async fn invoke(
    &self,
    request: IncrementRequest,
) -> Result<IncrementResponse, AIOProtocolError>
```

The `recv` signature remains unchanged when error info is added by replacing the [original model](#sample-model) with the [enhanced model](#enhanced-model).
However, the `invoke` signature changes to the following:

```rust
pub async fn invoke(
    &self,
    request: IncrementRequest,
) -> Result<Result<IncrementResponse, CounterError>, AIOProtocolError>
```

This revised signature is more-or-less necessary so that user error information can be returned to the caller of `invoke`.

> Design alternative: The `invoke` signature change could be avoided by embedding a `CounterError` in the `nested_error` field of an `AIOProtocolError`.
> This would require client-side user code to do something like this:
>
> ```rust
> let Some(nested_error) = err.nested_error {
>     print!("Increment error = {}", nested_error);
>     if let Some(counter_error) = nested_error.downcast_ref::<CounterError>() {
>         print!("error condition = {:?}", counter_error.condition);
>     }
> }
> ```

### Rust Server-side code

Here is an example of server-side user-code that calls `recv` and processes the received request.
It normally calls the `IncrementResponseBuilder::payload()` method to return a response, but when it encounters a problem, it instead calls `IncrementResponseBuilder::error()`.

```rust
let request = increment_executor.recv().await.unwrap().unwrap();
let mut response_builder = IncrementResponseBuilder::default();

let response = match counter_values.get(&request.payload.counter_name) {
    Some(current_value) => {
        if *current_value < i32::MAX {
            let new_value = *current_value + 1;
            counter_values.insert(request.payload.counter_name.clone(), new_value);
            response_builder
                .payload(IncrementResponsePayload {
                    counter_value: new_value,
                })
                .unwrap()
                .build()
                .unwrap()
        } else {
            response_builder
                .error(CounterError {
                    condition: Some(ConditionSchema::CounterOverflow),
                    explanation: Some(format!(
                        "Counter {} has saturated; no further increment is possible",
                        &request.payload.counter_name
                    )),
                })
                .unwrap()
                .build()
                .unwrap()
        }
    }
    None => response_builder
        .error(CounterError {
            condition: Some(ConditionSchema::CounterNotFound),
            explanation: Some(format!(
                "Counter {} not found in counter collection",
                &request.payload.counter_name
            )),
        })
        .unwrap()
        .build()
        .unwrap(),
};

request.complete(response).await.unwrap();
```

The server-side `IncrementResponseBuilder` generated from the [original model](#sample-model) provides a `payload()` method that passes the user's payload directly to an embedded builder that accepts the response:

```rust
pub fn payload(
    &mut self,
    payload: IncrementResponsePayload,
) -> Result<&mut Self, AIOProtocolError> {
    self.inner_builder.payload(payload)?;
    Ok(self)
}
```

For the [enhanced model](#enhanced-model), the `IncrementResponseBuilder` provides a `payload()` method that wraps the user's payload in a generated response object, and it also provides an `error()` method that wraps the user's error information in this same type of object.
The object is serialized and returned to the client:

```rust
pub fn payload(
    &mut self,
    payload: IncrementResponsePayload,
) -> Result<&mut Self, AIOProtocolError> {
    self.inner_builder.payload(IncrementResponseSchema {
        counter_value: Some(payload.counter_value),
        increment_error: None,
    })?;
    Ok(self)
}

pub fn error(&mut self, error: CounterError) -> Result<&mut Self, AIOProtocolError> {
    self.inner_builder.payload(IncrementResponseSchema {
        counter_value: None,
        increment_error: Some(error),
    })?;
    Ok(self)
}
```

### Rust Client-side code

Generated client-side code checks whether the response received from the server contains a result counter value or an increment error, and it returns the `Result` via the `Ok` variant or the `Err` variant as appropriate.
This user-level `Result` is embedded in the `Ok` variant of the overall `invoke` return value, which returns the `Err` variant for protocol-level errors.

```rust
pub async fn invoke(
    &self,
    request: IncrementRequest,
) -> Result<Result<IncrementResponse, CounterError>, AIOProtocolError> {
    let response = self.0.invoke(request).await;
    match response {
        Ok(response) => {
            if let Some(increment_error) = response.payload.increment_error {
                Ok(Err(increment_error))
            } else if let Some(counter_value) = response.payload.counter_value {
                Ok(Ok(IncrementResponse {
                    payload: IncrementResponsePayload { counter_value },
                    content_type: response.content_type,
                    format_indicator: response.format_indicator,
                    custom_user_data: response.custom_user_data,
                    timestamp: response.timestamp,
                }))
            } else {
                Err(AIOProtocolError {
                    message: Some(
                        "Command response has neither normal nor error payload content"
                            .to_string(),
                    ),
                    kind: AIOProtocolErrorKind::PayloadInvalid,
                    is_shallow: false,
                    is_remote: true,
                    nested_error: None,
                    header_name: None,
                    header_value: None,
                    timeout_name: None,
                    timeout_value: None,
                    property_name: None,
                    property_value: None,
                    command_name: Some("increment".to_string()),
                    protocol_version: None,
                    supported_protocol_major_versions: None,
                })
            }
        }
        Err(err) => Err(err),
    }
}
```

Here is example client-side code that invokes the command and is prepared for any of:

* on `Ok(Ok)` variant that contains the new counter value
* an `Ok(Err)` variant that contains a user-level `CounterError`
* an `Err` variant that indicates a protocol error

```rust
let increment_response = increment_invoker.invoke(increment_request).await;

match increment_response {
    Ok(Ok(increment_response)) => {
        print!(
            "Increment response = {}",
            increment_response.payload.counter_value
        );
    }
    Ok(Err(increment_error)) => {
        print!("Increment error = {}", increment_error);
        print!("error condition = {:?}", increment_error.condition);
    }
    Err(err) => {
        print!("Protocol error = {err:?}");
    }
}
```

[1]: ./0015-remove-422-status-code.md
