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
| `ErrorCode` | `Field` | Indicates that the cotyped `Field` within a `Result/Object` defines a set of allowed values for the "AppErrCode" user property defined in ADR 21 |
| `ErrorInfo` | `Field` | Indicates that the cotyped `Field` within a `Result/Object` defines the schema for JSON-serialized values in the "AppErrPayload" user property defined in ADR 21 |

Use of these new types is illustrated [below](#enhanced-model).

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
              "schema": {
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
  ]
}
```

The extant `Result` adjunct type indicates that the `Object` that is modeled as the "schema" of the "response" is treated specially by the ProtocolCompiler, and each `Field` therein has a special meaning identified by its co-type.

As described in ADR 19, the `Field` co-typed `NormalResult` defines the result returned under normal (non-error) conditions.
If the above model were to include a `Field` co-typed `ErrorResult`, this would define the result returned under error conditions.

It is perfectly acceptable to use the `ErrorResult` adjunct type from ADR 19 in conjunction with the new adjunct types defined herein, but this example omits the `ErrorResult` for simplicity.
When there is no `ErrorResult`, the payload must always conform to the `NormalResult`.

### ErrorCode adjunct type

A `Field` that is co-typed `ErrorCode` must have a "schema" that is an `Enum` whose "valueSchema" is "string".
This `Enum` enumerates the allowed values for the "AppErrCode" user property defined in ADR 21.
Each "name" will be code-generated into a language-appropriate enum name, and the corresponding "enumValue" will be the string used for the "AppErrCode" header value.

If a modeled `Command` does not include an `ErrorCode` definition, user code is expected to provide a string value directly (as illustrated in ADR 21) instead of an enumerated value.

### ErrorInfo adjunct type

A `Field` that is co-typed `ErrorInfo` specifies the schema for information that will be communicated via the "AppErrPayload" user property defined in ADR 21.
In the above example, this schema is an array of strings.
The information provided by user code will be JSON-serialized into a string for the "AppErrPayload" header value.

Note that this JSON serialization is independent of the serialization format the model specifies for Command payloads.
In the above example, the "payloadFormat" property has the value "Avro/1.11.0", indicating that AVRO serialization is used for Command (and Telemetry) payloads.
To ensure that header values are legal UTF8 strings, JSON serialization is always used for the "AppErrPayload" value, as prescribed by ADR 19.

If a modeled `Command` does not include an `ErrorInfo` definition, user code is expected to provide a JSON-encoded string value directly (as illustrated in ADR 21) instead of a strongly typed value conformant to an `ErrorInfo` schema.

[1]: ./0019-codegen-user-errs.md
[2]: ./0021-error-modeling-headers.md
