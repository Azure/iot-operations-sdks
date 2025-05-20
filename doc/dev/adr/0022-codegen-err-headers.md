# ADR 22: Modeling Error Headers

## Context

[ADR 19][1] defines DTDL and codegen support for modeling user errors in MQTT payloads.
[ADR 21][2] describes a facility for including user error information in MQTT headers, but it does not address how these headers could be modeled in DTDL.
The present ADR enhances the modeling features defined in ADR 19 to enable modeling the user error headers described in ADR 21.

## Decision

The DTDL Mqtt extension, which is version 3 as of the implementation of ADR 19, will be enhanced as described herein, yielding version 4 of this extension.
Implementing this mechanism will require changes to the ProtocolCompiler.

## MQTT extension, version 4

To enable models to express error headers in a way that can be understood by the ProtocolCompiler, the following new adjunct types are proposed for version 4 of the DTDL Mqtt extension.

| Adjunct Type | Material Cotype | Meaning |
| --- | --- | --- |
| `ErrorCode` | `Field` | Indicates that the cotyped `Field` within a `Result/Object` or an `Error/Object` defines a set of allowed values for the "AppErrCode" user property defined in ADR 21 |
| `ErrorInfo` | `Field` | Indicates that the cotyped `Field` within a `Result/Object` or an `Error/Object` defines the schema for JSON-serialized values in the "AppErrPayload" user property defined in ADR 21 |

Use of these new types on `Field`s in a `Result/Object` is illustrated [below](#enhanced-model), and their use on on `Field`s in an `Error/Object` is illustrated [further below](#enhanced-model-with-errorresult).

## Sample model

The following DTDL model defines an "increment" command with a response schema that is an integer value named "counterValue".
This is identical to the sample model in [ADR 19][1] except that the payload format has been changed from JSON to AVRO.
The payload format was not especially relevant to ADR 19, but it is relevant to the present ADR.

```json
{
  "@context": [ "dtmi:dtdl:context;4", "dtmi:dtdl:extension:mqtt;2" ],
  "@id": "dtmi:com:example:CounterCollection;1",
  "@type": [ "Interface", "Mqtt" ],
  "commandTopic": "rpc/command-samples/{executorId}/{commandName}",
  "payloadFormat": "Avro/1.11.0",
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

The following DTDL model enhances the above model with error header information that is cotyped with some [extant adjunct types](./0019-codegen-user-errs.md#mqtt-extension-version-3) and the proposed [new adjunct types](#mqtt-extension-version-4).

```json
{
  "@context": [ "dtmi:dtdl:context;4", "dtmi:dtdl:extension:mqtt;4" ],
  "@id": "dtmi:com:example:CounterCollection;1",
  "@type": [ "Interface", "Mqtt" ],
  "commandTopic": "rpc/command-samples/{executorId}/{commandName}",
  "payloadFormat": "Avro/1.11.0",
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
              "@type": [ "Field", "ErrorCode" ],
              "name": "appErrCode",
              "schema": "dtmi:com:example:CounterCollection:AppErrCode;1"
            },
            {
              "@type": [ "Field", "ErrorInfo" ],
              "name": "appErrPayload",
              "schema": {
                "@type": "Array",
                "elementSchema": "string"
              }
            }
          ]
        }
      }
    }
  ],
  "schemas": [
    {
      "@id": "dtmi:com:example:CounterCollection:AppErrCode;1",
      "@type": "Enum",
      "valueSchema": "string",
      "enumValues": [
        {
          "name": "success",
          "enumValue": "succès"
        },
        {
          "name": "failure",
          "enumValue": "échec"
        }
      ]
    }
  ]
}
```

The extant `Result` adjunct type indicates that the `Object` that is modeled as the "schema" of the "response" is treated specially by the ProtocolCompiler, and each `Field` therein has a special meaning identified by its co-type.

As described in ADR 19, the `Field` co-typed `NormalResult` defines the result returned under normal conditions.
If the above model were to include a `Field` co-typed `ErrorResult`, this would define the result returned under error conditions.
When there is no `ErrorResult`, the response payload must always conform to the `NormalResult`, even when there is an error.

It is perfectly acceptable to use a `Field` co-typed `ErrorResult` within the same `Result/Object` as `Field`s co-typed with the new adjunct types defined herein, but this example omits the `ErrorResult` for simplicity.

Note that the absence of an `ErrorResult` permits the "payloadFormat" property to specify "raw/0" or "custom/0", even though these formats normally require the Command "response" value to have a "schema" of "bytes".
Using a "response" "schema" of `Object/Result` with no `ErrorResult` and with a `NormalResult` "schema" of "bytes" permits the mechanism of the present ADR to define header values for raw and custom-serialized responses.

### ErrorCode adjunct type

A `Field` that is co-typed `ErrorCode` must have a "schema" that is an `Enum` whose "valueSchema" is "string".
This `Enum` enumerates the allowed values for the "AppErrCode" user property defined in ADR 21.
Each "name" will be code-generated into a language-appropriate enum name, and the corresponding "enumValue" will be the string used for the "AppErrCode" header value.

The `Field`'s "name" value ("appErrCode" in the above example) has no relevance to the communication, but it may affect the name of a code-generated type.

The specific usage of the generated enum type will vary according to programming language.
For the C# code described in ADR 21, the `WithApplicationError` method on `ExtendedResponse` will have a code-generated form that accepts the enum type instead of a raw string for the error code.

If a modeled `Command` does not include an `ErrorCode` definition, user code is expected to provide a string value directly (as illustrated in ADR 21) instead of an enumerated value.

### ErrorInfo adjunct type

A `Field` that is co-typed `ErrorInfo` specifies the schema for information that will be communicated via the "AppErrPayload" user property defined in ADR 21.
In the above example, this schema is an array of strings.
The information provided by user code will be JSON-serialized into a string for the "AppErrPayload" header value.

Note that this JSON serialization is independent of the serialization format the model specifies for Command payloads.
In the above example, the "payloadFormat" property has the value "Avro/1.11.0", indicating that AVRO serialization is used for Command (and Telemetry) payloads.
To ensure that header values are legal UTF8 strings, JSON serialization is always used for the "AppErrPayload" value, as prescribed by ADR 19.

The `Field`'s "name" value ("appErrPayload" in the above example) has no relevance to the communication, but it may affect the name of a code-generated type.

The specific usage of the generated type will vary according to programming language.
For the C# code described in ADR 21, the `WithApplicationError` method on `ExtendedResponse` will have a code-generated form that accepts the generated type instead of a serialized JSON string for the error info.

If a modeled `Command` does not include an `ErrorInfo` definition, user code is expected to provide a JSON-encoded string value directly (as illustrated in ADR 21) instead of a strongly typed value conformant to an `ErrorInfo` schema.

## Enhanced model with ErrorResult

The previous section described the use of adjunct types `ErrorCode` and `ErrorInfo` within an `Object` co-typed `Result`.
These adjunct types may also be used within an `Object` co-typed `Error`.

The following DTDL model is similar to the above enhanced model, but it adds a `Field` with co-type `ErrorResult` to the `Object/Result`, similar to the example in ADR 19.
Also, the `Field`s with new adjunct types have been relocated from the `Object/Result` to the `Object/Error` that is the "schema" of the `ErrorReslt`.

```json
{
  "@context": [ "dtmi:dtdl:context;4", "dtmi:dtdl:extension:mqtt;4" ],
  "@id": "dtmi:com:example:CounterCollection;1",
  "@type": [ "Interface", "Mqtt" ],
  "commandTopic": "rpc/command-samples/{executorId}/{commandName}",
  "payloadFormat": "Avro/1.11.0",
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
          "@type": [ "Field", "ErrorCode" ],
          "name": "appErrCode",
          "schema": "dtmi:com:example:CounterCollection:AppErrCode;1"
        },
        {
          "@type": [ "Field", "ErrorInfo" ],
          "name": "appErrPayload",
          "schema": {
            "@type": "Array",
            "elementSchema": "string"
          }
        }
      ]
    },
    {
      "@id": "dtmi:com:example:CounterCollection:AppErrCode;1",
      "@type": "Enum",
      "valueSchema": "string",
      "enumValues": [
        {
          "name": "success",
          "enumValue": "succès"
        },
        {
          "name": "failure",
          "enumValue": "échec"
        }
      ]
    }
  ]
}
```

As described in ADR 19, the error processing path may differ from the normal processing path.
For instance, in C#, the server indicates an error by throwing an exception type that is generated from the `Object` co-typed `Error`.
The use of an exception prevents the server code from calling the `WithApplicationError` method because the `ExtendedResponse` is instantiated within the generated exception handler.
Similarly, on the client side, exception-handling code has no access to the `ExtendedResponse`, so it is unable to call the `TryGetApplicationError` method thereon.

With the addition of `Field`s with co-types `ErrorCode` and `ErrorInfo` to the `Object/Error`, the exception type is generated with properties for setting and extracting values of the error headers.

[1]: ./0019-codegen-user-errs.md
[2]: ./0021-error-modeling-headers.md
