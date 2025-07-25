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
This requires an additive change to the DTDL Mqtt extension, and since version 4 of this extension has not officially shipped, this change will be rolled into the pending Mqtt extension version 4.

## Property roles and semantics

Just as RPC encompasses the two roles of Executor and Invoker, and as Telemetry encompasses the two roles of Sender and Receiver, the Property pattern encompasses two roles: Maintainer and Consumer.

The Maintainer's responsibilities are to hold the Property state, to read and update the state upon request, and to configurably distribute notifications of state changes.

The Consumer issues requests to the Maintainer, and it processes change notifications it receives from the Maintainer.

The values of all Properties within an Interface must be readable atomically, writable atomically, and notifiable atomically.
User code is not under any obligation to respect this atomicity, but the envoys are obligated to provide an API that readily supports atomicity if the application demands it.

There are five specific actions that are relevant to Properties:

* Action *write*:
  * The Consumer sends a `write` request to the Maintainer, specifying new values for one or more Properties.
  * The Maintainer attempts to apply the `write` and responds with an indication of which Properties were updated.
  * The Consumer receives the response from the Maintainer.
* Action *read*:
  * The Consumer sends a `read` request to the Maintainer, specifying which Properties it wishes to read.
  * The Maintainer attempts to read values for the designated Properties and responds with a collation of Property values.
  * The Consumer receives the response from the Maintainer.
* Action *observe*:
  * The Consumer sends a `observe` request to the Maintainer, indicating Properties to add to the notification list.
  * The Maintainer attempts to apply the `observe` and responds with an indication of which Properties are now on the notification list.
  * The Consumer receives the response from the Maintainer.
* Action *unobserve*:
  * The Consumer sends an `unobserve` request to the Maintainer, indicating Properties to remove from the notification list.
  * The Maintainer attempts to apply the `unobserve` and responds with an indication of which Properties remain on the notification list.
  * The Consumer receives the response from the Maintainer.
* Action *notify*:
  * Values of one or more Properties held by the Maintainer are modified, either by the application of a *write* action, or by an internal state change, or by some other mechanism.
  * The Maintainer broadcasts a change notification containing the current Property values.
  * The notification is received by all Consumers that are listening for notifications about the Properties.

The *write*, *read*, *observe*, and *unobserve* actions have behaviors that align with the RPC communication pattern.
The *notify* action has a behavior that aligns with the Telemetry communication pattern.

## Property envoy

A Property envoy is an assemblage of four Command envoys and one Telemetry envoy.
It is parameterized by three types: `TProp`, `TCtrl`, and `TStat`.
These types must satisfy the following constraints.

* `TProp` &mdash; a structure type (class/struct/object) that collates related Properties
  * There is one field for each static Property, which may have an arbitrary type
  * There is one field for each dynamically itemized Property, with a map/dictionary type
    * The dictionary key type is string
    * The dictionary value type may be an arbitrary type
* `TCtrl` &mdash; a structure type whose field names match those in `TProp`
  * Each static Property field has type Boolean
  * Each dynamically itemized Property field is an array/vector/list type with element type string
* `TStat` &mdash; a structure type whose field names match those in `TProp`
  * There is some type `TStatVal` for all fields that is Boolean, integer, string, or an enum
  * Each static Property field has type `TStatVal`
  * Each dynamically itemized Property field is a map/dictionary type
    * The dictionary key type is string
    * The dictionary value type is `TStatVal`

As an example, consider the following types.
These illustrations use C#, but analogous types can be defined in all supported languages.
Class `PropObj` is a concrete type for `TProp`:

```csharp
public partial class PropObj
{
    public int? Foo { get; set; } = default;
    public string? Bar { get; set; } = default;
    public Dictionary<string, int?> Baz { get; set; } = new();
}
```

Class `CtrlObj` is a concrete type for `TCtrl` that aligns with `PropObj`:

```csharp
public partial class CtrlObj
{
    public bool Foo { get; set; } = default;
    public bool Bar { get; set; } = default;
    public List<string> Baz { get; set; } = new();
}
```

Class `StatObj` is a concrete type for `TStat` that aligns with `PropObj` and has `TStatVal` of integer:

```csharp
public partial class StatObj
{
    public int? Foo { get; set; } = default;
    public int? Bar { get; set; } = default;
    public Dictionary<string, int> Baz { get; set; } = new();
}
```

The above types are used in the RPC and Telemetry realizations of Property actions.

## Envoy implementation

Continuing to illustrate in C#, the client-side `PropertyConsumer` is implemented as follows:

```csharp
public class PropertyWriteRequester<TProp, TStat> : CommandInvoker<TProp, TStat>;

public class PropertyReadRequester<TCtrl, TProp> : CommandInvoker<TCtrl, TProp>;

public class PropertyObserveRequester<TCtrl, TStat> : CommandInvoker<TCtrl, TStat>;

public class PropertyUnobserveRequester<TCtrl, TStat> : CommandInvoker<TCtrl, TStat>;

public class PropertyListener<TProp> : TelemetryReceiver<TProp>;
```

The service-side `PropertyMaintainer` is implemented as follows:

```csharp
public class PropertyWriteResponder<TProp, TStat> : CommandExecutor<TProp, TStat>;

public class PropertyReadResponder<TCtrl, TProp> : CommandExecutor<TCtrl, TProp>;

public class PropertyObserveResponder<TCtrl, TStat> : CommandExecutor<TCtrl, TStat>;

public class PropertyUnobserveResponder<TCtrl, TStat> : CommandExecutor<TCtrl, TStat>;

public class PropertyNotifier<TProp> : TelemetrySender<TProp>;
```

The *write* action is performed by issuing a 'write' Command request.

* The request payload is an instance of `TProp`.
  * For statically itemized properties, the optional fields in the `TProp` request object contain values for any Properties that are to be written.
  * For dynamically itemized properties, the keys in the request map indicate which Properties to write, and the corresponding values are what is to be written.
* The response payload is an instance of `TStat`.
  * For statically itemized properties, each optional field in the `TStat` response object indicates the status of the write operation for the Property named by the field.
  * For dynamically itemized properties, each value in the response map indicates the status of the write operation for the Property named by the key.
  * The semantics of `TStatVal` values are determined by the service that implements the property.

The *read* action is performed by issuing a 'read' Command request.

* The request payload is an instance of `TCtrl`.
  * For statically itemized properties, the optional fields in the `TCtrl` request object are `true` for each field whose Property is to be read.
  * For dynamically itemized properties, the elements in the request array indicate the names of the Properties to read.
* The response payload is an instance of `TProp`.
  * For statically itemized properties, the optional fields in the `TProp` response object contain values for any Properties that have been read.
  * For dynamically itemized properties, the keys in the response map indicate which Properties have been read, and the corresponding values are what was read.

The *observe* action is performed by issuing an 'observe' Command request.

* The request payload is an instance of `TCtrl`.
  * For statically itemized properties, the optional fields in the `TCtrl` request object are `true` for each field whose Property is to be added to the notify list.
  * For dynamically itemized properties, the elements in the request array indicate the names of the Properties to add to the notify list.
  * The maintainer may keep a single notify list across all consumers, or it may keep a separate notify list for each consumer; in the latter case, the maintainer must be provided with a way to identify the consumer that requested the observation.
* The response payload is an instance of `TStat`.
  * For statically itemized properties, each optional field in the `TStat` response object indicates the status of the observe operation for the Property named by the field.
  * For dynamically itemized properties, each value in the response map indicates the status of the observe operation for the Property named by the key.
  * The semantics of `TStatVal` values are determined by the service that implements the property.
  * The maintainer's response may additionally provide status values for the observation state of properties other than those named in the 'observe' request.

The *unobserve* action is performed by issuing an 'unobserve' Command request.

* The request payload is an instance of `TCtrl`.
  * For statically itemized properties, the optional fields in the `TCtrl` request object are `true` for each field whose Property is to be removed from the notify list.
  * For dynamically itemized properties, the elements in the request array indicate the names of the Properties to remove from the notify list.
  * The maintainer may keep a single notify list across all consumers, or it may keep a separate notify list for each consumer; in the latter case, the maintainer must be provided with a way to identify the consumer that requested the observation.
* The response payload is an instance of `TStat`.
  * For statically itemized properties, each optional field in the `TStat` response object indicates the status of the unobserve operation for the Property named by the field.
  * For dynamically itemized properties, each value in the response map indicates the status of the unobserve operation for the Property named by the key.
  * The semantics of `TStatVal` values are determined by the service that implements the property.
  * The maintainer's response may additionally provide status values for the observation state of properties other than those named in the 'unobserve' request.

The *notify* action is performed by sending a Telemetry with a payload that is an instance of `TProp`.

* For statically itemized properties, the optional fields in the `TProp` notify object contain values for any Properties that are in the notify list.
* For dynamically itemized properties, the key/value pairs in the notify map indicate which Properties are in the notify list and their corresponding values.

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
| `{modelId}` | The identifier of the service model, which is the full DTMI of the Interface, might include the version | optional |
| `{maintainerId}` | Identifier of the maintainer, by default the MQTT client ID | optional |
| `{sourceId}` | Identifier of the source of the request or notification, by default the MQTT client ID | optional |
| `{action}` | One of "read", "write", "observe", "unobserve", or "notify" | optional |

An example Property topic pattern is illustrated in the sample model below.

## Sample model

The following DTDL model defines three Properties, two of which ("Foo" and "Bar") are statically itemized, and one of which ("Props") is dynamically itemized.
These Properties correspond to the example types [defined above](#property-envoy).

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
      "@type": [ "Property", "Fragmented" ],
      "name": "Baz",
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

The ProtocolCompiler will aggregate all statically itemized Properties into a single set of classes.
These classes will be analogous to the definitions of `PropObj`, `CtrlObj`, and `StatObj` defined above, each having fields for Foo, Bar, and Baz, as in this example:

```csharp
public partial class PropObj
{
    public int? Foo { get; set; } = default;
    public string? Bar { get; set; } = default;
    public Dictionary<string, int?> Baz { get; set; } = new();
}
```
