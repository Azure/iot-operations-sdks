# ADR 26: WoT Protocol Binding for AIO

## Context

The AIO team is planning to change our primary modeling language from [Digital Twins Definition Language (DTDL)](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md) to [Web of Things (WoT) Thing Description (TD)](https://www.w3.org/TR/wot-thing-description/).
The AIO ProtocolCompiler currently ingests DTDL models that employ extension types and properties defined by the [DTDL Mqtt extension](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.mqtt.v3.md).
This extension enables models to express MQTT topic patterns, header value definitions, error annotations, and other information that is not included in core DTDL but which is needed for generating source-code binders that target the AIO SDKs.

Models written in WoT TD format will need to express the same information that is currently expressed via DTDL and its Mqtt extension.
WoT TD can directly express much of this information, but some things &mdash; such as MQTT topic patterns &mdash; have no native representation in WoT.
In addition, there are use cases that call for models to reference external schema definitions written in other schema formats.
External schemas are not supported by either DTDL or WoT, but we would like our chosen modeling language to provide this ability.

Much like DTDL's [language extension mechanism](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.Extensions.md), WoT provides for [Binding Templates](https://www.w3.org/TR/wot-binding-templates/) that define additional types and properties for expressing supplementary modeling information.
WoT defines three types of bindings:

* [Protocol Bindings](https://www.w3.org/TR/wot-binding-templates/#protocol-bindings) define reusable vocabulary and rules that map TD elements to protocol-specific message types and properties.
* [Payload Bindings](https://www.w3.org/TR/wot-binding-templates/#payload-bindings) define payload formats and media types that can be represented in a TD.
* [Platform Bindings](https://www.w3.org/TR/wot-binding-templates/#platform-bindings) combine the use of protocols and payloads to comprehensively describe application-specific Thing Models.

To enable WoT to achieve expressive parity with DTDL and its Mqtt extension, and to further provide support for external schema definitions, we need to define a new Protocol Binding.
This requires:

* Defining a new URI scheme and [registering it](https://www.ietf.org/archive/id/draft-ietf-iri-4395bis-irireg-02.html) with [IANA](https://www.iana.org/)
  * Alternatively, designating the extant URI scheme "dtmi:", when used as a protocol indicator, to imply communication via AIO
* Defining a WoT Ontology ([RDF vocabulary](https://www.w3.org/RDF/)) using [RDF Schema](https://www.w3.org/TR/rdf12-schema/)
* Writing a WoT Ontology document ([W3C Editor's Draft](https://www.w3.org/standards/types/#x2-3-editor-s-draft))
* Writing a WoT Protocol Binding Template document ([W3C Editor's Draft](https://www.w3.org/standards/types/#x2-3-editor-s-draft))

Before proceeding with the above formal steps, we need to at least informally define the types, properties, and rules that will be included in the AIO Protocol Binding.
The present ADR presents a proposal for this informal definition.

## Background

### DTDL

The top-level type in [DTDL](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md) is [Interface](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#interface), which can hold contents of five distinct types.
The [Telemetry](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#telemetry) and [Command](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#command) content types correspond directly to the Telemetry and RPC communication classes in AIO SDKs.
The [Property](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#property) content type has no direct analogue in the SDKs, but the ProtocolCompiler provides [Property support](https://github.com/Azure/iot-operations-sdks/blob/main/doc/dev/adr/0023-property-support.md) by targeting specialized forms of RPC for reading and writing.
The remaining two content types, [Relationship](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#relationship) and [Component](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#component), are not understood by the ProtocolCompiler and have no corresponding communication pattern.

The schemas of Property values, Telemetry messages, and Command requests/responses can be defined using a selection of [primitive schema types](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#primitive-schema) or model-defined specializations of complex schema types that incorporate primitive types and/or other complex type specializations.
The complex schema types provided by DTDL are [Array](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#array), [Object](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#object), [Enum](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#enum), and [Map](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#map).

#### DTDL Mqtt extension

The core DTDL language provides sufficient expressiveness to define common communication patterns and their data schemas.
However, it does not provide much of the detail needed for fully specified binders that instantiate the communication classes in the AIO SDKs.
To address this gap, the [DTDL Mqtt extension](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md) defines adjunct types and properties that enable a model to specify these required details:

* [telemServiceGroupId, cmdServiceGroupId](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#mqtt-adjunct-type) &mdash; properties for specifying service group IDs for shared subscriptions to Telemetry or Command topics
* [telemetryTopic, commandTopic, propertyTopic](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#mqtt-adjunct-type) &mdash; properties for specifing MQTT topic patterns for Telemetry messages, Command requests, and Property read/write requests
* [payloadFormat](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#mqtt-adjunct-type) &mdash; a property for designating a serialization format to use in an MQTT payload
* [Idempotent](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#idempotent-adjunct-type), [Cacheable](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#cacheable-adjunct-type) &mdash; adjunct types that designate a Command's semantics as idempotent or cacheable
* [Indirect](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#indirect-adjunct-type) &mdash; adjunct type indicating that an Object field should be indirectly linked to its parent Object; used to break loops in recursive schema structures
* [Transparent](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#transparent-adjunct-type) &mdash; adjunct type indicating that a redundant level of object nesting in a Command request or response should be elided
* [index](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#indexed-adjunct-type) &mdash; property to establish fixed index values for structure fields, which are used in some serialization formats
* [Fragmented](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#fragmented-adjunct-type) &mdash; adjunct type applied to a Property's Map schema to indicate that each key-value pair in the Map should be treated as a separate Property
* [Result, NormalResult, ErrorResult](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#result-normalresult-and-errorresult-adjunct-types) &mdash; adjunct types used to define error responses to Command requests
* [PropertyResult, PropertyValue, ReadError, WriteError](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#propertyresult-propertyvalue-readerror-and-writeerror-adjunct-types) &mdash; adjunct types used to define error responses to Property reads and writes
* [Error, ErrorMessage](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#error-and-errormessage-adjunct-types) &mdash; adjunct types to indicate that an Object represents an error value and that a designated field in the Object provides a textual description of the error condition
* [ErrorCode, ErrorInfo](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#errorcode-and-errorinfo-adjunct-types) &mdash; adjunct types that define values for MQTT headers to relay error information in Command responses

### WoT TD

The WoT Thing Description format defines types and properties that correspond to _almost_ all of the core DTDL types that are understood by the ProtocolCompiler:

* [Thing](https://www.w3.org/TR/wot-thing-description/#thing) &mdash; the top-level type in WoT, which corresponds to a DTDL Interface
* [events](https://www.w3.org/TR/wot-thing-description/#eventaffordance), [actions](https://www.w3.org/TR/wot-thing-description/#actionaffordance), [properties](https://www.w3.org/TR/wot-thing-description/#propertyaffordance) &mdash; properties whose values define affordances analogous to DTDL Telemetries, Commands, and Properties, respectively
* [ArraySchema](https://www.w3.org/TR/wot-thing-description/#arrayschema) &mdash; a complex schema type analogous to DTDL Array
* [ObjectSchema](https://www.w3.org/TR/wot-thing-description/#objectschema) &mdash; a complex schema type analogous to DTDL Object
* [enum](https://www.w3.org/TR/wot-thing-description/#dataschema) &mdash; a property that declares a restricted set of values for a schema, analogous to DTDL Enum

> Note: There is no WoT type or property analogous to DTDL Map

The WoT Thing Description format has several properties that express analogous semantics to aspects of the DTDL Mqtt extension:

* [contentType](https://www.w3.org/TR/wot-thing-description/#form-contentType) &mdash; provides the same information as the DTDL Mqtt extension property payloadFormat
* [idempotent](https://www.w3.org/TR/wot-thing-description/#actionaffordance) &mdash; has identical meaning to the DTDL Mqtt extension type Idempotent
* [safe](https://www.w3.org/TR/wot-thing-description/#actionaffordance) &mdash; has identical meaning to the DTDL Mqtt extension type Cacheable
* [additionalResponses](https://www.w3.org/TR/wot-thing-description/#form-additionalResponses) &mdash; subsumes the DTDL Mqtt extension types ErrorResult, ReadError, and WriteError

Some types and properties defined by the DTDL Mqtt extension are not needed in a WoT TD:

* Result, NormalResult, PropertyResult, PropertyValue &mdash; structural types that provide anchors for ErrorResult, ReadError, and WriteError
* Indirect &mdash; provides support for recursive schemas, which are not permitted in WoT TDs
* Transparent &mdash; workaround for a semantic limitation in DTDL
* index &mdash; not appropriate for a Protocol Binding; should be included in Payload Bindings for formats that require structure indices
* Error &mdash; indicates an error type, which can be inferred from other information

## Proposal

### URI scheme, protocol, and RDF term prefix

To indicate communication via AIO, the proposal is to use the [IANA-registered](https://www.iana.org/assignments/uri-schemes/uri-schemes.xhtml) URI scheme "dtmi:" as a protocol indicator.
At present, there is no protocol corresponding to this URI scheme; it is used only for [identifiers](https://github.com/Azure/opendigitaltwins-dtdl/tree/master/DTMI) within DTDL models.
Since there is no competing protocol meaning for this URI scheme, there is no risk of ambiguity if we designate it as a protocol indicator for AIO.

The proposed RDF term prefix is "dtv:", derived from scheme "dtmi:" according to the "v suffix" notation [recommended](https://www.w3.org/TR/wot-binding-templates/#creating-a-new-protocol-binding-template-subspecification) for vocabulary creation.

### AIO Protocol Binding

The proposed AIO Protocol Binding introduces 7 new RDF terms.
The first TD example illustrates 4 terms that are used within "forms" elements, namely "dtv:topic", "dtv:serviceGroupId", "dtv:headerCode", and "dtv:headerInfo".

```json
"actions": {
  "increment": {
    "input": {
      . . .
    },
    "output": {
      . . .
    },
    "forms": [
      {
        "op": "invokeaction",
        "href": "dtmi:communicationTest:counterCollection:increment;1",
        "contentType": "application/json",
        "dtv:topic": "test/CounterCollection/increment",
        "dtv:serviceGroupId": "CounterCmdGroup",
        "additionalResponses": [
          {
            "success": false,
            "contentType": "application/json",
            "schema": "CounterError"
          }
        ],
        "dtv:headerCode": [
          "success",
          "counterNotFound",
          "counterOverflow"
        ],
        "dtv:headerInfo": [
          {
            "success": false,
            "contentType": "application/json",
            "schema": "CounterInfo"
          }
        ]
      }
    ]
  }
}
```

The above example shows an "increment" action that uses the AIO protocol.
The protocol is implied by the "dtmi:" scheme in the URI that is the value of "href".

The MQTT topic used for the increment request is "test/CounterCollection/increment", and the service group ID for shared subscriptions is "CounterCmdGroup".
These are indicated by the values of "dtv:topic" and "dtv:serviceGroupId", respectively.

In the action response, the MQTT user header ["AppErrCode"](https://github.com/Azure/iot-operations-sdks/blob/main/doc/dev/adr/0021-error-modeling-headers.md) is constrained by the "dtv:headerCode" property to have one of the following string values: "success", "counterNotFound", and "counterOverflow".

The MQTT user header ["AppErrPayload"](https://github.com/Azure/iot-operations-sdks/blob/main/doc/dev/adr/0021-error-modeling-headers.md) is defined by the "dtv:headerInfo" property to be a JSON string satisfying schema CounterInfo.
The CounterInfo schema is defined in the "schemaDefinitions" section of the TD (shown below), in the same manner as schemas used within values of "additionalResponses".

The second TD example illustrates the term "dtv:errorMessage", which is used inside a value within the "schemaDefinitions" object.

```json
"schemaDefinitions": {
  "CounterError": {
    "descriptions": {
      "en": "The requested counter operation could not be completed."
    },
    "type": "object",
    "additionalProperties": false,
    "dtv:errorMessage": "explanation",
    "properties": {
      "explanation": {
        "type": "string"
      },
      "details": {
        "type": "string"
      }
    }
  },
  "CounterInfo": {
    "type": "object",
    "additionalProperties": false,
    "properties": {
      "errorCount": {
        "type": "integer"
      }
    }
  },
}
```

The "schemaDefinitions" section of a TD is used to define schemas that can be referenced by the "additionalResponses" property of a "forms" element within the TD.
Commonly, these additional responses are used to define error responses.
The DTDL Mqtt extension uses the [Error and ErrorMessage](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#error-and-errormessage-adjunct-types) adjunct types to indicate that an Object represents an error value and that a designated field in the Object provides a textual description of the error condition.
This information is used by the ProtocolCompiler to generate programming-language-appropriate mechanisms for encapsulating error information and relaying an error message.

In the proposed Protocol Binding, when an object schema in a "schemaDefinitions" value contains a "dtv:errorMessage" property, the value of this property indicates the name of an object property that will be used to convey an error message, analogous to the ErrorMessage type in the DTDL Mqtt extension.
The presence of the "dtv:errorMessage" property can be used to infer that the containing object is an error object, so no type property analogous to the Mqtt extension adjunct type Error is needed.

The third and final example illustrates 2 terms that are used within an affordance definition, namely "dtv:ref", "dtv:placeholder".

```json
"properties": {
  "subcomponents": {
    "dtv:ref": "./schemas/Subcomponent.schema.json",
    "readOnly": true,
    "dtv:placeholder": true,
    "forms": [
      {
        "op": "readproperty",
        "href": "dtmi:communicationTest:counterCollection:subcomponents;1",
        "contentType": "application/json"
      }
    ]
  }
}
```

The "dtv:ref" property is used in place of other properties (such as "type") that are used to define the schema of an event, action, or property.
The value of "dtv:ref" is a reference to an external schema definition.
The reference is a URI or file path that is relative to the location of the TD definition that contains the reference.
The schema format of the external schema must align with the schema type indicated by the "contentType" property in the affordance's "forms" values.
For example, if the "contentType" is "application/json", the format of the external schema definition must be JSON Schema.

When the "dtv:placeholder" boolean property is included and assigned a value of true, this indicates that the event, action, or property is a placeholder for a dynamic collection of events, actions, or properties.
Placeholders ([optional](https://reference.opcfoundation.org/Core/Part3/v105/docs/6.4.4.4.4) and [mandatory](https://reference.opcfoundation.org/Core/Part3/v105/docs/6.4.4.4.5)) are a feature of OPC UA.
Since we intend to map OPC UA companion specifications to WoT TDs, the TD requires a way to indicate when an affordance is a placeholder for a collection.

### Mapping DTDL to WoT

The following table summarizes the mapping between DTDL and WoT as proposed above.

| DTDL Type/Property | DTDL Core or Extension | WoT Semantic Equivalent | WoT Native or Binding |
| --- | --- | --- | --- |
| Interface | DTDL core | Thing | WoT native |
| Telemetry | DTDL core | events | WoT native |
| Command | DTDL core | actions | WoT native |
| Property | DTDL core | properties | WoT native |
| Array | DTDL core | ArraySchema | WoT native |
| Object | DTDL core | ObjectSchema | WoT native |
| Enum | DTDL core | enum | WoT native |
| Map | DTDL core | (via external schema) | WoT AIO Protocol Binding |
| telemServiceGroupId | DTDL Mqtt extension | dtv:serviceGroupId | WoT AIO Protocol Binding |
| cmdServiceGroupId | DTDL Mqtt extension | dtv:serviceGroupId | WoT AIO Protocol Binding |
| telemetryTopic | DTDL Mqtt extension | dtv:topic | WoT AIO Protocol Binding |
| commandTopic | DTDL Mqtt extension | dtv:topic | WoT AIO Protocol Binding |
| propertyTopic | DTDL Mqtt extension | dtv:topic | WoT AIO Protocol Binding |
| payloadFormat | DTDL Mqtt extension | contentType | WoT native |
| Idempotent | DTDL Mqtt extension | idempotent | WoT native |
| Cacheable | DTDL Mqtt extension | safe | WoT native |
| Indirect | DTDL Mqtt extension | (unneeded semantic) | &mdash; |
| Transparent | DTDL Mqtt extension | (unneeded workaround) | &mdash; |
| index | DTDL Mqtt extension | (relegated) | per WoT Payload Binding |
| Fragmented | DTDL Mqtt extension | dtv:placeholder | WoT AIO Protocol Binding |
| Result | DTDL Mqtt extension | (unneeded structure) | &mdash; |
| NormalResult | DTDL Mqtt extension | (unneeded structure) | &mdash; |
| ErrorResult | DTDL Mqtt extension | additionalResponses | WoT native |
| PropertyResult | DTDL Mqtt extension | (unneeded structure) | &mdash; |
| PropertyValue | DTDL Mqtt extension | (unneeded structure)  | &mdash; |
| ReadError | DTDL Mqtt extension | additionalResponses | WoT native |
| WriteError | DTDL Mqtt extension | additionalResponses | WoT native |
| Error | DTDL Mqtt extension | (inferred) | &mdash; |
| ErrorMessage | DTDL Mqtt extension | dtv:errorMessage | WoT AIO Protocol Binding |
| ErrorCode | DTDL Mqtt extension | dtv:headerCode | WoT AIO Protocol Binding |
| ErrorInfo | DTDL Mqtt extension | dtv:headerInfo | WoT AIO Protocol Binding |
| (external schema ref) | (not supported) | dtv:ref | WoT AIO Protocol Binding |
