# ADR 23: SDK Support for Property

## Context

The AIO SDKs currently support two communication patterns: RPC and Telemetry.
There is no support for the stateful communication pattern we call Property.
There is an upcoming need to support this pattern because it is used heavily in OPC UA companion specifications, which AIO intends to support during [Br].

Although one design option is to support Property without any SDK modifications, by providing support entirely within the ProtocolCompiler, this would be unsatisfactory for non-codegen scenarios.
Another design option is for the SDKs to provide native support for Property that is parallel to &mdash; and independent of &mdash; their support for RPC and Telemetry; however, this would be very expensive in terms of development cost.

## Decision

The AIO SDKs will be enhanced with new envoy classes that support the Property communication pattern.
Because the semantics of Property are decomposable into semantics of RPC and Telemetry, the new Property envoys will leverage the extant RPC and Telemetry envoys by layering functionality atop them.

The ProtocolCompiler will be enhanced to support the DTDL Property content type.
Generated code will target the new Property envoy classes in the SDKs.
This requires a minimal change to the DTDL Mqtt extension, and since version 4 of this extension has not officially shipped, this additive change will be rolled into the pending Mqtt extension version 4.

## Property roles and semantics

Just as RPC encompasses the two roles of Executor and Invoker, and as Telemetry encompasses the two roles of Sender and Receiver, the Property pattern encompasses two roles: Maintainer and Consumer.

The Maintainer's responsibilities are to hold the Property state, to read and update the state upon request, and to configurably distribute notifications of state changes.

The Consumer issues requests to the Maintainer, and it processes change notifications it receives from the Maintainer.

The values of all Properties within an Interface must be readable atomically, writable atomically, and notifiable atomically.
User code is not under any obligation to respect this atomicity, but the envoys are obligated to provide an API that readily supports atomicity if the application demands it.

There are five specific actions that are relevant to Properties:

* Action *write*:
  * The Consumer sends a `write` request to the Maintainer, specifying new values for one or more Properties.
  * The Maintainer attempts to apply the `write` and responds with an indication of which Properties were updated; if an error condition occurs, the Maintainer instead responds with an error indication.
  * The Consumer receives the response from the Maintainer.
  * Note: A request to write a read-only Property does not trigger an error condition; instead, the response merely indicates that the read-only Property was not updated.
* Action *read*:
  * The Consumer sends a `read` request to the Maintainer, specifying which Properties it wishes to read.
  * The Maintainer attempts to read values for the designated Properties and responds with a collation of Property values; if an error condition occurs, the Maintainer instead responds with an error indication.
  * The Consumer receives the response from the Maintainer.
* Action *watch*:
  * The Consumer sends a `watch` request to the Maintainer, indicating Properties to add to the notification list.
  * The Maintainer attempts to apply the `watch` and responds with an indication of which Properties are now on the notification list; if an error condition occurs, the Maintainer instead responds with an error indication.
  * The Consumer receives the response from the Maintainer.
* Action *unwatch*:
  * The Consumer sends an `unwatch` request to the Maintainer, indicating Properties to remove from the notification list.
  * The Maintainer attempts to apply the `unwatch` and responds with an indication of which Properties remain on the notification list; if an error condition occurs, the Maintainer instead responds with an error indication.
  * The Consumer receives the response from the Maintainer.
* Action *notify*:
  * Values of one or more Properties held by the Maintainer are modified, either by the application of a *write* action, or by an internal state change, or by some other mechanism.
  * The Maintainer broadcasts a change notification containing the current Property values.
  * The notification is received by all Consumers that are listening for notifications about the Properties.

The *write*, *read*, *watch*, and *unwatch* actions have behaviors that align with the RPC communication pattern.
The *notify* action has a behavior that aligns with the Telemetry communication pattern.

## Property envoy

A Property envoy is an assemblage of four Command envoys and one Telemetry envoy.
It is parameterized by three types:

* `TProp` &mdash; A structure type (class/struct/object) that collates related Properties
  * There is one field in the `TProp` structure per Property in the collation.
  * Each field may have an arbitrary type.
  * In a `TProp` instance, the value of one or more fields may be omitted.
* `TBool` &mdash; a structure type whose field names match those in `TProp`
  * Each field has type Boolean.
* `TErr` &mdash; an arbitrary type that conveys error information
  * Used when an exceptional situation prevents a meaningful `TProp` or `TBool` result.

As an example, consider the following types.
These illustrations use C#, but analogous types can be defined in all supported languages.
Class `AggregateProp` is a concrete type for `TProp`:

```csharp
    public partial class AggregateProp
    {
        public int? Foo { get; set; } = default;
        public string? Bar { get; set; } = default;
    }
```

Class `ControlProp` is a concrete type for `TBool` that aligns with `AggregateProp`:

```csharp
    public partial class ControlProp
    {
        public bool Foo { get; set; } = default;
        public bool Bar { get; set; } = default;
    }
```

There are no restrictions on `TErr`, so any concrete type can serve as an example:

```csharp
public partial class PropError
{
    public ConditionSchema? Condition { get; set; } = default;
    public string? Explanation { get; set; } = default;
}
```

The above types are used both directly and indirectly in the RPC and Telemetry realizations of Property actions.
Their indirect usage is as values for type parameters in the following generic type, which is used for responses to *write*, *read*, *watch*, and *unwatch* actions:

```csharp
class Result<TNorm, TErr>
{
    public TNorm? NormalResult { get; set; }
    public TErr? ErrorResult { get; set; }
}
```

The `TErr` type parameter must conform to the `TErr` abstract type described above.
The `TNorm` type parameter may conform to either the `TProp` or `TBool` abstract type, depending on the action.

## Envoy implementation

Continuing to illustrate in C#, the client-side `PropertyConsumer` is implemented as follows:

```csharp
public class PropertyWriteRequester<TProp, TBool, TErr> : CommandInvoker<TProp, Result<TBool, TErr>>;

public class PropertyReadRequester<TProp, TBool, TErr> : CommandInvoker<TBool, Result<TProp, TErr>>;

public class PropertyWatchRequester<TBool, TErr> : CommandInvoker<TBool, Result<TBool, TErr>>;

public class PropertyUnwatchRequester<TBool, TErr> : CommandInvoker<TBool, Result<TBool, TErr>>;

public class PropertyListener<TProp> : TelemetryReceiver<TProp>;
```

The service-side `PropertyMaintainer` is implemented as follows:

```csharp
public class PropertyWriteResponder<TProp, TBool> : CommandExecutor<TProp, Result<TBool, TErr>>;

public class PropertyReadResponder<TProp, TBool> : CommandExecutor<TBool, Result<TProp, TErr>>;

public class PropertyWatchResponder<TBool> : CommandExecutor<TBool, Result<TBool, TErr>>;

public class PropertyUnwatchResponder<TBool> : CommandExecutor<TBool, Result<TBool, TErr>>;

public class PropertyNotifier<TProp> : TelemetrySender<TProp>;
```

The *write* action is performed by issuing a 'write' Command request.
The payload is an instance of `TProp`, whose optional fields contain values for any Properties that are to be written.
In non-error conditions, the response payload is an instance of `TBool`, which has a value of `true` for each field whose Property was updated.

The *read* action is performed by issuing a 'read' Command request.
The `TBool` payload has a value of `true` for each field whose Property is to be read.
The non-error `TProp` response payload contains values in fields for any Properties that are read.

The *watch* action is performed by issuing a 'watch' Command request.
The `TBool` payload has a value of `true` for each field whose Property is to be added to the notify list.
The non-error `TBool` response payload conveys the updated notify list; each field has a value of `true` if its corresponding Property was added to the list by this action or if it was already in the notify list.

The *unwatch* action is performed by issuing an 'unwatch' Command request.
The `TBool` payload has a value of `true` for each field whose Property is to be removed from the notify list.
The non-error `TBool` response payload conveys the updated notify list; each field has a value of `true` if the corresponding Property was previously in the notify list and was not removed by this action.

The *notify* action is performed by sending a Telemetry.
The `TProp` payload contains values in fields for any Properties that are in the notify list.

## Additive change to DTDL Mqtt extension

To support MQTT communication of DTDL Property contents, the Mqtt extension will be updated.
Specifically, when the Mqtt adjunct type co-types an Interface, it adds properties `commandTopic`, `telemetryTopic`, and [several others](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.mqtt.v3.md#mqtt-adjunct-type).
As part of the present change, an additional property will be added:

| Property | Required | Data type | Limits | Description |
| --- | --- | --- | --- | --- |
| `propertyTopic` | optional | *string* | slash-separated sequence of character-restricted labels and/or brace-enclosed tokens | MQTT topic pattern on which a request or notification is published. |

The DTDL Mqtt extension places no restrictions &mdash; other than basic syntactical constraints &mdash; on the set of tokens used in MQTT topic patterns.
Topic tokens recognized by the ProtocolCompiler are defined in the [topic-structure](https://github.com/Azure/iot-operations-sdks/blob/main/doc/reference/topic-structure.md) document.
The sets of tokens differ between RPC and Telemetry, and the following set of tokens is hereby defined for Property:

| Topic token | Description | Required |
| --- | --- | --- |
| `{modelId}` | The identifier of the the service model, which is the full DTMI of the Interface, might include the version | optional |
| `{maintainerId}` | Identifier of the maintainer, by default the MQTT client ID | optional |
| `{sourceId}` | Identifier of the source of the request or notification, by default the MQTT client ID | optional |
| `{action}` | One of "read", "write", "watch", "unwatch", or "notify" | optional |

An example Property topic pattern is illustrated in the sample model below.

## Sample model

The following DTDL model defines three Properties, which correspond to the example types [defined above](#property-envoy).

```json
  {
    "@context": [ "dtmi:dtdl:context;4", "dtmi:dtdl:extension:mqtt;4" ],
    "@id": "dtmi:propertySketch:PropertySketch;1",
    "@type": [ "Interface", "Mqtt" ],
    "payloadFormat": "Json/ecma/404",
    "propertyTopic": "sample/{modelId}/{sourceId}/property/{action}",
    "contents": [
      {
        "@type": "Property",
        "name": "Foo",
        "schema": "integer"
      },
      {
        "@type": "Property",
        "name": "Bar",
        "schema": "string"
      },
      {
        "@type": "Property",
        "name": "PropError",
        "schema": {
          "@type": [ "Object", "Error" ],
          "description": "The requested operation could not be completed.",
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
                    "name": "persistentFailure",
                    "enumValue": 1
                  },
                  {
                    "name": "temporaryFailure",
                    "enumValue": 2
                  }
                ]
              }
            }
          ]
        }
      }
    ]
  }
```

Recall that the `AggregateProp` class has fields for Foo and Bar.
The ProtocolCompiler will generate an analogous class from the two Properties named "Foo" and "Bar" in the above model:

```csharp
    public partial class AggregateProp
    {
        public int? Foo { get; set; } = default;
        public string? Bar { get; set; } = default;
    }
```

The ProtocolCompiler will also generate a class analogous to `ControlProp`:

```csharp
    public partial class ControlProp
    {
        public bool Foo { get; set; } = default;
        public bool Bar { get; set; } = default;
    }
```

The model Property named "PropError" is not represented in either of the above two classes.
This is because its "schema" value is an Object with co-type Error, indicating that this is a definition of an error value.
Consequently, it results in the following generated class:

```csharp
public partial class PropError
{
    public ConditionSchema? Condition { get; set; } = default;
    public string? Explanation { get; set; } = default;
}
```
