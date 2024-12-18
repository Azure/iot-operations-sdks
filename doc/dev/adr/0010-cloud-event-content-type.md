# ADR10: Setting Cloud Event Fields

## Status

PROPOSED

## Context

This ADR addresses several issues:

**First** and most critically, our SDKs do not properly serialize the CloudEvents `datacontenttype` attribute.
At present, our SDKs map the CloudEvents `datacontenttype` attribute to an MQTT PUBLISH User Property field named 'datacontenttype'.
However, the document [MQTT Protocol Binding for CloudEvents - Version 1.0.2](https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/bindings/mqtt-protocol-binding.md#314-examples) states:

> The CloudEvents `datacontenttype` attribute is mapped to the MQTT PUBLISH `Content Type` field; all other CloudEvents attributes are mapped to MQTT PUBLISH User Property fields.

**Second**, the Media Broker needs the ability to directly set the values of all Cloud Event fields.
At present, our Go SDK does provide this facility; however, our .NET and Rust SDKs do not enable user code to set values for the `id`, `time`, `subject`, or `datacontenttype` fields.

**Third**, our SDKs are not consistent with each other on which fields they validate and what validations they perform:

* The .NET SDK ensures that:
  * `source` is a URL
  * `time` is a time
* The Go SDK ensures that:
  * `specversion` is "1.0"
  * `source` is non-empty
* The Rust SDK ensures that:
  * `specversion` is "1.0"
  * all fields are non-empty
  * `source` is a URL
  * `dataschema` is a URL
  * `time` is a time

## Decision

All SDKs will be updated to enable all Cloud Events fields to be set by user code.

The current default rules, which are consistently implemented in all SDKs, will be maintained:

* There is no default value for `source`; if it is not set, no CloundEvent will be included
* The default value for `type` is "ms.aio.telemetry"
* The default value for `specversion` is "1.0"
* There is no default value for `dataschema`
* The default value for `id` is a newly generated GUID
* The default value for `time` is the current time
* The default value for `subject` is the Telemetry topic
* The default value for `datacontenttype` is the content type indicated by the serializer

All SDKs will be corrected to serialize `datacontenttype` to/from the MQTT PUBLISH `Content Type` field.

All SDKs will perform the following validations of Cloud Event fields, which will ensure conformance with the [CloudEvents - Version 1.0.2](https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md) specification:

* `source` is a URL
* `type` is non-empty
* `specversion` is non-empty
* `dataschema`, if present, is a URI
* `id` is a GUID
* `subject`, if present, is a non-empty string
* `datacontenttype` conforms to [RFC2046](https://datatracker.ietf.org/doc/html/rfc2046)
* `time`, if present, is a time tht will serializate per [RFC3339](https://datatracker.ietf.org/doc/html/rfc3339)

A set of METL test cases will be written to ensure that the above behaviors are implemented consistently across SDKs.

## Consequences

A key consequence of this decision is that the MQTT PUBLISH `Content Type` field will be determined by the user setting -- or the absence thereof -- for the Cloud Event `datacontenttype` field:

* If the Cloud Event `datacontenttype` field has a value set by user code, this will be serialized to the MQTT PUBLISH `Content Type` field.
* If the Cloud Event `datacontenttype` field is not set by user code, it will default to the content type indicated by the serializer, which will then be serialized to the MQTT PUBLISH `Content Type` field.

## Alternatives Considered

There are no alternatives under consideration for addressing the improper serialization of the CloudEvents `datacontenttype` attribute.

There are no alternatives under consideration for addressing the inconsistencies across SDK implementations.

Several alternatives have been considered to address the need for Media Broker to set the `Content Type` field.
The approaches differ by how they express the content type:

1. an implicit association with a Telemetry sender/receiver pair, with one pair per media type
2. a parameter or option on the Telemetry send method
3. a property on an object passed into the Telemetry send method


The first alternative requires significant development effort and would likely be awkward to use.
The second and third approaches require changes to the SDK API that are regarded as inappropriate.
All alternatives fail to address the other issues addressed by this ADR.
