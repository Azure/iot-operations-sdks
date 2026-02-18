# ADR 29: WoT Platform Binding for AIO

## Context

The AIO team is in the process of changing our primary modeling language from [Digital Twins Definition Language (DTDL)](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md) to [Web of Things (WoT) Thing Model (TM)](https://www.w3.org/TR/wot-thing-description/#introduction-tm).

DTDL supports language extensions that define additional types and properties usable in models.
One such extension is the Azure IoT Operations extension, which has been defined but not released.
The purpose of this extension is to enable DTDL models to express information appropriate for the AIO Golden Path, whose semantics are defined in the [Common Information Model](https://microsoft.sharepoint.com/:w:/t/DigitalOperations/IQDUq0xTOkMoSImWfPd5AH8oAR8OWsRmL3Be0epTA6j0DT0?e=3IWPqy) document.

> NOTE: Because the Azure IoT Operations extension has not been released, its documentation is not openly accessible.
> The docs are therefore unlinkable from this document without causing linkspector violations.
> People with access can find the docs on the GitHub Azure/DTDL repo at path "dtdl/Docs/generated/en-US/DTDL.iotoperations.v4.md".

As with DTDL models, WoT Thing Models need to express AIO Golden Path information.
Much like DTDL's [language extension mechanism](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.Extensions.md), WoT provides for [Binding Templates](https://www.w3.org/TR/wot-binding-templates/) that define additional types and properties for expressing supplementary modeling information.
WoT defines three types of bindings:

* [Protocol Bindings](https://www.w3.org/TR/wot-binding-templates/#protocol-bindings) define reusable vocabulary and rules that map Thing Description or Thing Model elements to protocol-specific message types and properties.
* [Payload Bindings](https://www.w3.org/TR/wot-binding-templates/#payload-bindings) define payload formats and media types that can be represented in a Thing Description.
* [Platform Bindings](https://www.w3.org/TR/wot-binding-templates/#platform-bindings) combine the use of protocols and payloads to comprehensively describe application-specific Thing Models.

The AIO team has informally defined a [WoT Protocol Binding for AIO](./0026-wot-protocol-binding.md), which enables Thing Models to express MQTT topic patterns, header value definitions, error annotations, and other information needed for generating source-code binders that target the AIO SDKs, analogously to the [Mqtt extension](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.mqtt.v3.md) for DTDL models.


For Golden Path support, we need a WoT Platform Binding for AIO, which will enable Thing Models to express Golden Path information analogously to the AIO extension for DTDL models.
This requires:

* Defining a WoT Ontology ([RDF vocabulary](https://www.w3.org/RDF/)) using [RDF Schema](https://www.w3.org/TR/rdf12-schema/)
* Writing a WoT Ontology document ([W3C Editor's Draft](https://www.w3.org/standards/types/#x2-3-editor-s-draft))
* Writing a WoT Platform Binding Template document ([W3C Editor's Draft](https://www.w3.org/standards/types/#x2-3-editor-s-draft))

Before proceeding with the above formal steps, we need to at least informally define the types, properties, and rules that will be included in the AIO Platform Binding.
The present ADR presents a proposal for this informal definition.

## Background

### DTDL

The top-level type in [DTDL](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md) is [Interface](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#interface), which can hold contents of five distinct types.

* [Telemetry](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#telemetry) is a unidirectional server-to-client communication pattern.

* [Command](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#command) is a bidirectional RPC pattern.
Its semantics are implemented on the server side by a Command Executor and invoked from the client side by a Command Invoker.

* [Property](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#property) is a stateful communication pattern in which state is held on the server side by a Property Maintainer.
This state can be accessed by a Property Consumer on the client side.

* [Relationship](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#relationship) is a means for declaring a semantic association between instances of DTDL models.
It has no directly analogous communication pattern.
Like the other types of contents, a Relationship has a name defined by the model.

* [Component](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#component) is a structural mechanism for expressing model composition.
It is mentioned here for completeness, but it has no usage in AIO.

#### DTDL Azure IoT Operations extension

The Azure IoT Operations extension defines the following extension types and properties.

##### Extension types

* Composite &mdash; a cotype applied to an Interface to indicate that the Interface is not a component or capability of any other Interface.

* Event &mdash; a cotype applied to an Interface to indicate that the Interface has semantics associated with an event type.

* HasCapability &mdash; a cotype applied to a Relationship to indicate that the Relationship target is a capability (as opposed to a component, a collection, a feed, a sink, etc.) of the Relationship source.

* HasComponent &mdash; a cotype applied to a Relationship to indicate that the Relationship target is a component (as opposed to a capability, a collection, a feed, a sink, etc.) of the Relationship source.

* Detail and Subject &mdash; cotypes that work together to indicate an object structure that conveys a subject value along with detail information about the subject.

##### Extension properties

DTDL does not allow for freestanding properties that are independent of type.
Every extension property is defined to have an associated domain that is an extension cotype, and this cotype must be applied to a native type for the property to be usable on that type.
The following extension properties are listed along with the cotypes of their domains.

* typeRef (Congruence) &mdash; specifies an opaque reference to another type definition (usually in a different modeling format) that is congruent to this definition.

* namespace (via Qualified) &mdash; specifies a namespace that qualifies the `name` property of the model element.

* scaleFactor (via ScaledStatically) &mdash; specifies a constant value used for statically scaling instance values.

* decimalPlaces (via Precise) &mdash; specifies the significant precision of a numeric value.

* group (via GroupMember) &mdash; for a Command, specifies the name of a group of which the Command is a member.

* maximum and minimum (via Limited) &mdash; specifies maximum and minimum expected values of a numeric type that is thereby limited to the specified range.

## Proposal

### URI and RDF term prefix

The proposed RDF term prefix is "aov:", derived from the phrase "Azure Operations" and constructed in accordance with the "v suffix" notation [recommended](https://www.w3.org/TR/wot-binding-templates/#creating-a-new-protocol-binding-template-subspecification) for vocabulary creation.

### AIO Platform Binding

The proposed AIO Platform Binding introduces 14 new RDF terms:

* `aov:isComposite` &mdash; property whose value is `true` when a TM is not a component or capability of any other TM.
* `aov:isEvent` &mdash; property whose value is `true` when a TM has semantics associated with an event type.
* `aov:capability` &mdash; value for `rel` property in a `links` element indicating that the linked TM is a capability of this TM.
* `aov:component` &mdash; value for `rel` property in a `links` element indicating that the linked TM is a component of this TM.
* `aov:reference` &mdash; value for `rel` property in a `links` element indicating an untyped reference to the linked TM.
* `aov:typedReference` &mdash; value for `rel` property in a `links` element indicating a typed reference to the linked TM; the type must be given by an `aov:refType` property.
* `aov:refType` &mdash; property whose string value indicates a user-defined reference type for a `links` element whose `rel` property has value `aov:typedReference`.
* `aov:refName` &mdash; property whose string value indicates a name for the reference to the linked TM; can be used irrespective of the link `rel` value.
* `aov:contains` &mdash; property whose value is the name of another affordance that is logically contained within this affordance.
* `aov:containedIn` &mdash; property whose value is the name of another affordance that logically contains this affordance.
* `aov:typeRef` &mdash; property whose value is an opaque identifier of another type definition that is congruent to this definition.
* `aov:namespace` &mdash; property whose value is a namespace that qualifies the name of the model element.
* `aov:scaleFactor` &mdash; property whose value is used for scaling instance values.
* `aov:decimalPlaces` &mdash; property whose value specifies the significant precision of a numeric value.
* `aov:memberOf` &mdash; property whose value specifies the name of a group of which this action is a member.

Use of these is illustrated in the following TM examples.

#### Example 1

The following TM example illustrates 6 terms: `aov:isComposite`, `aov:component`, `aov:reference`, `aov:refName`, `aov:scaleFactor`, `aov:decimalPlaces`, and `aov:namespace`.

The model is designated as composite.
There are two `links` elements, one with name "filter" that indicates a component, and one with name "acoustics" that indicates an untyped reference.
The numeric value of the "Gain" property has a scale factor of 10.0, and it has 3 decimal places of precision.
The name "Gain" is qualified by the namespace "MyControls".

```json
{
  "@context": [
    "https://www.w3.org/2022/wot/td/v1.1",
    { "aov": "http://azure.com/IoT/operations/tm#" },
    { "dtv": "http://azure.com/DigitalTwins/dtmi#" }
  ],
  "@type": "tm:ThingModel",
  "aov:isComposite": true,
  "title": "Amplifier",
  "links": [
    { "rel": "aov:component", "aov:refName": "filter", "href": "./BandPassFilter.TM.json", "type": "application/tm+json" },
    { "rel": "aov:reference", "aov:refName": "acoustics", "href": "./AcousticModel.TM.json", "type": "application/tm+json" }
  ],
  "properties": {
    "Gain": {
      "aov:namespace": "MyControls",
      "type": "number",
      "aov:scaleFactor": 10.0,
      "aov:decimalPlaces": 3,
      "forms": [
        {
          "contentType": "application/json",
          "dtv:topic": "samples/property/gain/{action}",
          "op": [ "readproperty", "writeproperty" ]
        }
      ]
    }
  }
}
```

#### Example 2

The following TM example illustrates 5 terms: `aov:isEvent`, `aov:capability`, `aov:typedReference`, `aov:refType`, and `aov:memberOf`.

The model is designated as an event.
There are two `links` elements, one that indicates a capability, and one that indicates a typed reference with user-defined type "MySpecialReference".
The action "Noop" is designated to be a member of the action group "MyActionGroup".

```json
{
  "@context": [
    "https://www.w3.org/2022/wot/td/v1.1",
    { "aov": "http://azure.com/IoT/operations/tm#" },
    { "dtv": "http://azure.com/DigitalTwins/dtmi#" }
  ],
  "@type": "tm:ThingModel",
  "aov:isEvent": true,
  "title": "Calculator",
  "links": [
    { "rel": "aov:capability", "href": "./OperationHistory.TM.json", "type": "application/tm+json" },
    { "rel": "aov:typedReference", "aov:refType": "MySpecialReference", "href": "./DigitalDevice.TM.json", "type": "application/tm+json" }
  ],
  "actions": {
    "Noop": {
      "forms": [
        {
          "contentType": "application/json",
          "dtv:topic": "samples/action/noop",
          "op": "invokeaction"
        }
      ],
      "aov:memberOf": "MyActionGroup"
    }
  }
}
```


#### Example 3

The following TM example illustrates 3 terms: `aov:contains`, `aov:containedIn`, and `aov:typeRef`.

Property "ControlSignal" indicates that it contains properties "OperatingDirection" and "Setpoint".
Correspondinly, properties "OperatingDirection" and "Setpoint" each indicate containment within property "ControlSignal".
(It is not necessary to specify both directions, but if both are specified, they must be consistent.)
Each of the properties has a type reference to an identifier in the space of OPC UA node IDs.

```json
{
  "@context": [
    "https://www.w3.org/2022/wot/td/v1.1",
    { "aov": "http://azure.com/IoT/operations/tm#" },
    { "dtv": "http://azure.com/DigitalTwins/dtmi#" }
  ],
  "@type": "tm:ThingModel",
  "title": "Controller",
  "properties": {
    "ControlSignal": {
      "aov:typeRef": "nsu=http://opcfoundation.org/UA/PADIM/;i=1023",
      "type": "number",
      "aov:contains": [ "OperatingDirection", "Setpoint" ],
      "forms": [ { "dtv:topic": "opcua/PADIM/ControlSignal" } ]
    },
    "OperatingDirection": {
      "aov:typeRef": "nsu=http://opcfoundation.org/UA/PADIM/;i=1195",
      "type": "integer",
      "aov:containedIn": "ControlSignal",
      "forms": [ { "dtv:topic": "opcua/PADIM/OperatingDirection" } ]
    },
    "Setpoint": {
      "aov:typeRef": "nsu=http://opcfoundation.org/UA/PADIM/;i=1196",
      "type": "number",
      "aov:containedIn": "ControlSignal",
      "forms": [ { "dtv:topic": "opcua/PADIM/Setpoint" } ]
    }
  }
}
```

### Mapping DTDL to WoT

A semantic mapping between core DTDL and native WoT can be found [here](https://github.com/digitaltwinconsortium/ManufacturingOntologies/blob/main/comparison.md).

The following table summarizes the mapping between the DTDL AIO extension and the WoT Platform Binding for AIO.

| DTDL Core Type | DTDL AIO Extension Cotype/Property | WoT Native Property | WoT Binding Property |
| --- | --- | --- | --- |
| Telemetry | | events | |
| Command | | actions | |
| Property | | properties | |
| Interface | Composite | | aov:isComposite |
| Interface | Event | | aov:isEvent |
| Relationship | | links / rel | aov:reference / aov:refName |
| Relationship | (not supported) | links / rel | aov:typedReference / aov:refType / aov:refName |
| Relationship | HasCapability | links / rel | aov:capability / aov:refName |
| Relationship | HasComponent | links / rel | aov:component / aov:refName |
| Object | Detail / Subject | | aov:contains / aov:containedIn |
| | Congruence.typeRef | | aov:typeRef |
| | Qualified.namespace | | aov:namespace |
| | ScaledStatically.scaleFactor | | aov:scaleFactor |
| | Precise.decimalPlaces | | aov:decimalPlaces |
| Command | GroupMember.group | | aov:memberOf |
| | Limited.maximum | maximum | |
| | Limited.minimum | minimum | |
