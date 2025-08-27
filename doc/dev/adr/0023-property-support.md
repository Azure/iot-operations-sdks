# ADR 23: Codegen Support for Property

## Context

The ProtocolCompiler currently generates code from the two DTDL content types Command and Telemetry.
There is no support for the content type Property, which represents a stateful communication pattern.
There is an upcoming need to support this pattern because it is used heavily in OPC UA companion specifications, which are translated into DTDL models.

The AIO SDKs provide first-class support for RPC (Command) and Telemetry communication patterns but no support for the Property pattern.
However, the semantics of Property (i.e., reading and writing) are achievable via special cases of the RPC pattern.

## Requirements

In its most basic form, a Property can be realized as two Commands, one for reading and one for writing.
(In fact, for read-only Properties, only one Command is needed.)
However, the situation is complicated by three requirements.

1. **optional aggregation** &mdash; Just as Telemetry can support either sending all of an Interface's Telemetries together or sending each Telemetry on a separate MQTT topic, it must be possible either to read and write all of an Interface's Properties in a consistent snapshot or to read and write each Property separately.

2. **error modeling** &mdash; Mechanisms analogous to those defined by [ADR 19](./0019-codegen-user-errs.md) must be available to enable users to model errors that can be returned when a read or write request fails.

3. **dynamically itemized properties** &mdash; In addition to supporting conventional Properties that are defined statically at modeling time, there must be a way to model placeholders for collections of Properties that are itemized dynamically at run time.

## Decision

The ProtocolCompiler will be enhanced to support the DTDL Property content type.
No modifications will be made to the AIO SDKs.
Instead, extant SDK mechanisms will be targeted by the ProtocolCompiler.
This requires an additive change to the DTDL Mqtt extension, and since version 4 of this extension has not officially shipped, this change will be rolled into the pending Mqtt extension version 4.

## Property roles and semantics

Just as RPC encompasses the two roles of Executor and Invoker, and as Telemetry encompasses the two roles of Sender and Receiver, the Property pattern encompasses two roles: Maintainer and Consumer.
The Maintainer's responsibilities are to hold the Property state, to read the state upon request, and (for writable properties) to update the state upon request.
The Consumer issues read and (if appropriate) write requests to the Maintainer.

## Additive changes to DTDL Mqtt extension

To support MQTT communication of DTDL Property contents, the Mqtt extension will be updated with three additive changes, which are described in the following three subsections.

### New property `propertyTopic` added to the Mqtt adjunct type

When the Mqtt adjunct type co-types an Interface, it adds properties `commandTopic`, `telemetryTopic`, and [several others](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.mqtt.v3.md#mqtt-adjunct-type).
As part of the present change, another property will be added:

| Property | Required | Data type | Limits | Description |
| --- | --- | --- | --- | --- |
| `propertyTopic` | optional | *string* | slash-separated sequence of character-restricted labels and/or brace-enclosed tokens | MQTT topic pattern on which a read or write request is published. |

The DTDL Mqtt extension places no restrictions &mdash; other than basic syntactical constraints &mdash; on the set of tokens used in MQTT topic patterns.
Topic tokens recognized by the ProtocolCompiler are defined in the [topic-structure](https://github.com/Azure/iot-operations-sdks/blob/main/doc/reference/topic-structure.md) document.
The sets of tokens differ between RPC and Telemetry, and the following set of tokens is hereby defined for Property:

| Topic token | Description | Required |
| --- | --- | --- |
| `{modelId}` | The identifier of the service model, which is the full DTMI of the Interface | optional |
| `{maintainerId}` | Identifier of the maintainer, by default the MQTT client ID | optional |
| `{consumerClientId}` | The MQTT client ID of the endpoint that requests to read or write a Property | optional |
| `{propertyName}` | The name of the Property | optional |
| `{action}` | "read" or "write" | optional |

>[!NOTE]
> The string value of the `propertyTopic` property is not required to contain the `{propertyName}` token, but Property communication differs depending on whether this token is present:
>
> * If the `propertyTopic` property contains a `{propertyName}` token, each Property is assigned a separate MQTT pub/sub topic, and each is read or written separately.
> * If the `propertyTopic` property does not contain a `{propertyName}` token, all Properties in the Interface are grouped into a collection that is read or written with a combined payload. This does not require updating all Properties with each write operation, but it provides the option of writing multiple Properties in a single consistent update.

>[!NOTE]
> The action token is optional because a model might have no writable properties, in which case all requests are implicitly read requests.

#### Basic sample model with separate Property topics

The following sample model defines two Properties, one read-only and one writable.
The property topic pattern contains the tokens "{propertyName}" and "{action}".

```json
{
  "@context": [ "dtmi:dtdl:context;4", "dtmi:dtdl:extension:mqtt;4" ],
  "@id": "dtmi:propertySketch:PropertySketch;1",
  "@type": [ "Interface", "Mqtt" ],
  "payloadFormat": "Json/ecma/404",
  "propertyTopic": "sample/property/{propertyName}/{action}",
  "contents": [
    {
      "@type": "Property",
      "name": "Foo",
      "schema": "integer"
    },
    {
      "@type": "Property",
      "name": "Bar",
      "writable": true,
      "schema": "string"
    }
  ]
}
```

This model generates three Commands:

| Command name | MQTT topic | Example request | Example response |
| --- | --- | --- | --- |
| ReadFoo | "sample/property/Foo/read" | | { "Foo": 33 } |
| ReadBar | "sample/property/Bar/read" | | { "Bar": "hello" } |
| WriteBar | "sample/property/Bar/write" | { "Bar": "bye" } | |

The two read Commands have no request payloads.
Their response payloads are objects, each with a single required field.
The reverse holds for the write Command, which is defined only for the writable property.

In C#, the generated code would look something like this:

```csharp
public partial class FooProperty
{
    public int Foo { get; set; }
}

public partial class BarProperty
{
    public string Bar { get; set; }
}
```

```csharp
public class FooPropertyReadRequester : CommandInvoker<EmptyJson, FooProperty>;

public class BarPropertyReadRequester : CommandInvoker<EmptyJson, BarProperty>;

public class BarPropertyWriteRequester : CommandInvoker<BarProperty, EmptyJson>;
```

#### Basic sample model with aggregated Property topics

The next sample model is identical to the above except for the property topic value, which does not contain a "{propertyName}" token:

```json
  "propertyTopic": "sample/property/{action}"
```

This model generates two Commands:

| Command name | MQTT topic | Example request | Example response |
| --- | --- | --- | --- |
| Read | "sample/property/read" | | { "Foo": 33, "Bar": "hello" } |
| Write | "sample/property/write" | { "Bar": "bye" } | |

In general, a model that does not contain a "{propertyName}" token generates at most two Commands, one for reading and one for writing.
If no Property in the Interface is writable, only a read Command is generated.

The read Command has no request payload, and the write Command has no response payload.
Fields in the read response are not optional and not nullable; all values are read on each read Command.
Fields in the write request are optional; only fields that are present and have non-null values will be written.

>[!NOTE]
> This optionality/nullability is defined by the code-generation process and is not affected in any way by the user's model.
> There is no way for a user to add (or remove) optionality/nullability at the top level of a Property.
> If such control is desired, the model must define an Object as the schema of the Property, allowing selectable optionality on the Fields within the Object.

In C#, the generated code would look something like this:

```csharp
public partial class PropertyCollection
{
    public int Foo { get; set; }
    public string Bar { get; set; }
}

public partial class WritablePropertyCollection
{
    public string? Bar { get; set; }
}
```

```csharp
public class PropertyCollectionReadRequester : CommandInvoker<EmptyJson, PropertyCollection>;

public class PropertyCollectionWriteRequester : CommandInvoker<WritablePropertyCollection, EmptyJson>;
```

### New adjunct types: PropertyResult, PropertyValue, ReadError, and WriteError

The second additive change to the Mqtt extension is support for modeling the error conditions that can result from a Property read or write.
Mechanisms for modeling errors in Command responses are defined in [ADR 19](./0019-codegen-user-errs.md).
The present ADR adds closely analogous mechanisms for modeling errors in Property read/write responses.
Specifically, the following four adjunct types are added.

| New Adjunct Type | Material Cotype | Analogous ADR19 Type | On-wire usage |
| --- | --- | --- | --- |
| PropertyResult | Object | Result | Schema of Read/Write response |
| PropertyValue | Field | NormalResult | Read response when no error, Write request |
| ReadError | Field | ErrorResult | Read response when error |
| WriteError | Field | ErrorResult | Write response when error |

These adjunct types behave nearly identically, *mutatis mutandis*, to the analogous types defined for Command in ADR19.
Specifically:

* PropertyResult defines the type sent on the wire in a read response.
* PropertyResult also defines the type sent on the wire in a write response, except there is no payload when there is no error.
* PropertyValue defines the type returned by the Read API when there is no error. *
* PropertyValue also defines the type accepted by the Write API for writable Properties. *
* ReadError defines the type returned by the Read API when an error occurs during reading.
* WriteError defines the type returned by the Write API when an error occurs during writing.

> \* The types generated from PropertyValue for Read and Write are not exactly the same when there is an aggregated Property topic.
> In this case, each field in the value accepted by the Write API will be optional/nullable, whereas the fields in the value returned by the Read API will not be.

In addition, the schema of a Field that is co-typed ReadError or WriteError must be an Object that is co-typed with the extant Error adjunct type.

The following sample model illustrates the use of these new adjunct types in an enhancement of the above model.

```json
{
  "@context": [ "dtmi:dtdl:context;4", "dtmi:dtdl:extension:mqtt;4" ],
  "@id": "dtmi:propertySketch:PropertySketch;1",
  "@type": [ "Interface", "Mqtt" ],
  "payloadFormat": "Json/ecma/404",
  "propertyTopic": "sample/property/{propertyName}/{action}",
  "contents": [
    {
      "@type": "Property",
      "name": "Foo",
      "schema": {
        "@type": [ "Object", "PropertyResult" ],
        "fields": [
          {
            "@type": [ "Field", "PropertyValue" ],
            "name": "foo",
            "schema": "integer"
          },
          {
            "@type": [ "Field", "ReadError" ],
            "name": "propError",
            "schema": "dtmi:com:example:FooPropertyError;1"
          }
        ]
      }
    },
    {
      "@type": "Property",
      "name": "Bar",
      "writable": true,
      "schema": {
        "@type": [ "Object", "PropertyResult" ],
        "fields": [
          {
            "@type": [ "Field", "PropertyValue" ],
            "name": "bar",
            "schema": "string"
          },
          {
            "@type": [ "Field", "ReadError", "WriteError" ],
            "name": "propError",
            "schema": "dtmi:com:example:BarPropertyError;1"
          }
        ]
      }
    }
  ]
}
```

Note that the "propError" field of Property "Bar" is co-typed with both ReadError and WriteError.
This is not a requirement.
Writable Properties may define separate error result types for read and write Commands if desired.

In C#, the generated code for the above model would look something like this:

```csharp
public partial class FooPropertyReadResponseSchema
{
    public FooProperty? Foo { get; set; } = default;
    public FooPropertyError? PropError { get; set; } = default;
}

public partial class BarPropertyReadResponseSchema
{
    public BarProperty? Bar { get; set; } = default;
    public BarPropertyError? PropError { get; set; } = default;
}

public partial class BarPropertyWriteResponseSchema
{
    public BarPropertyError? PropError { get; set; } = default;
}
```

```csharp
public class FooPropertyReadRequester : CommandInvoker<EmptyJson, FooPropertyReadResponseSchema>;

public class BarPropertyReadRequester : CommandInvoker<EmptyJson, BarPropertyReadResponseSchema>;

public class BarPropertyWriteRequester : CommandInvoker<BarProperty, BarPropertyWriteResponseSchema>;
```

### New adjunct type: Fragmented

The third additive change to the Mqtt extension is support for dynamically itemized Properties via the new adjunct type Fragmented.

The following sample model defines a dynamically itemized, writable Property placeholder named Baz.

```json
{
  "@context": [ "dtmi:dtdl:context;4", "dtmi:dtdl:extension:mqtt;4" ],
  "@id": "dtmi:propertySketch:PropertySketch;1",
  "@type": [ "Interface", "Mqtt" ],
  "payloadFormat": "Json/ecma/404",
  "propertyTopic": "sample/property/{propertyName}/{action}",
  "contents": [
    {
      "@type": [ "Property", "Fragmented" ],
      "name": "Baz",
      "writable": true,
      "schema": {
        "@type": "Map",
        "mapKey": {
          "name": "propKey",
          "schema": "string"
        },
        "mapValue": {
          "name": "propValue",
          "schema": "integer"
        }
      }
    }
  ]
}
```

A dynamically itemized Property placeholder is defined using co-type Fragmented and a schema that is a Map whose contents are dynamically added Properties.
The Map keys are the names of the dynamic Properties, and the Map values are the Properties themselves.

The Fragmented adjunct type indicates that the Map values have analogous constraints to the Property values of normal static Properties.
Specifically, Map values in a read response are not nullable, and all values in the Map should be returned on each read Command.
Map values in a write request are optional and nullable:

* If a key is omitted from the Map in a write request, the value is not written.
* If a key is included and the value is non-null, the dynamic Property is added if not yet present and updated if already present.
* If a key is included and the value is null, the dynamic Property is removed from the Map.

If the above-defined error types are used with a dynamically itemized Property, the PropertyValue applies to the Map in aggregate, not to individual entries in the Map.

```json
{
  "@context": [ "dtmi:dtdl:context;4", "dtmi:dtdl:extension:mqtt;4" ],
  "@id": "dtmi:propertySketch:PropertySketch;1",
  "@type": [ "Interface", "Mqtt" ],
  "payloadFormat": "Json/ecma/404",
  "propertyTopic": "sample/property/{propertyName}/{action}",
  "contents": [
    {
      "@type": "Property",
      "name": "Baz",
      "writable": true,
      "schema": {
        "@type": [ "Object", "PropertyResult" ],
        "fields": [
          {
            "@type": [ "Field", "PropertyValue" ],
            "name": "baz",
            "schema": {
              "@type": "Map",
              "mapKey": {
                "name": "propKey",
                "schema": "string"
              },
              "mapValue": {
                "name": "propValue",
                "schema": "integer"
              }
            }
          },
          {
            "@type": [ "Field", "ReadError", "WriteError" ],
            "name": "propError",
            "schema": "dtmi:com:example:BazPropertyError;1"
          }
        ]
      }
    }
  ]
}
```

In C#, the generated code would look something like this:

```csharp
public partial class BazProperty
{
    public Dictionary<string, int> Baz { get; set; }
}

public partial class BazWritableProperty
{
    public Dictionary<string, int?> Baz { get; set; }
}
```

```csharp
public partial class BazPropertyReadResponseSchema
{
    public BazProperty? Baz { get; set; } = default;
    public BazPropertyError? PropError { get; set; } = default;
}

public partial class BazPropertyWriteResponseSchema
{
    public BazPropertyError? PropError { get; set; } = default;
}
```

```csharp
public class BazPropertyReadRequester : CommandInvoker<EmptyJson, BazPropertyReadResponseSchema>;

public class BazPropertyWriteRequester : CommandInvoker<BazWritableProperty, BazPropertyWriteResponseSchema>;
```

If a modeler wishes to return a Map of error conditions so that each condition can be paired with a dynamic Property in the Map, this must be defined manually.
In other words, the schema of the Error must be an Object containing a Map field, like this:

```json
{
  "@id": "dtmi:com:example:BazPropertyError;1",
  "@type": [ "Object", "Error" ],
  "fields": [
    {
      "@type": [ "Field", "ErrorMessage" ],
      "name": "explanation",
      "schema": "string"
    },
    {
      "name": "details",
      "schema": {
        "@type": "Map",
        "mapKey": {
          "name": "propKey",
          "schema": "string"
        },
        "mapValue": {
          "name": "errorCondition",
          "schema": "string"
        }
      }
    }
  ]
}
```
