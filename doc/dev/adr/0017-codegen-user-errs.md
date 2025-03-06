# ADR 17: Modeling User Errors

## Status

PROPOSED

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
* A [brief note](#code-generation-in-other-languages) on code-generation in other languages

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
It is now a composite result that normally contains an integer named "counterValue".

When the composite result reflects an error, it contains a collection of error information instead of the "counterValue" integer.
The specific error information is defined by the `Field` cotyped `ErrorResult`.
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
This code is generated because the "exception" field in the DTDL `Object` is cotyped `ErrorMessage`.

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
An alternative way to access this same value is via the `Explanation` property of the nested `CounterError`, but the example above ilustrates that the standard C# mechanism for conveying error strings is usable.

## Code generation in other languages

Details for code generation in Go and Rust have not yet been worked out.
The intention is for these languages to employ error mechanisms that are standard and conventional for the language, analogous to the mechanisms described above for C#.
This document will be updated with additional information for these other languages as the design is refined.

[1]: ./0015-remove-422-status-code.md
