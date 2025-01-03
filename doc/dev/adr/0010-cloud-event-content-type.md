# ADR10: Setting Cloud Event Fields

## Status

APPROVED

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
  * `dataschema` is a URL
  * `time` is a time
* The Rust SDK ensures that:
  * `specversion` is "1.0"
  * all fields are non-empty
  * `source` is a URL
  * `dataschema` is a URL
  * `time` is a time

## Decision

All SDKs will be updated to enable all Cloud Events fields to be set by user code.

The current default rules, which are consistently implemented in all SDKs, will be maintained:

* There is no default value for `source`; a value must be provided by user code
* The default value for `type` is "ms.aio.telemetry"
* The default value for `specversion` is "1.0"
* There is no default value for `dataschema`
* The default value for `id` is a newly generated GUID
* The default value for `time` is the current time
* The default value for `subject` is the Telemetry topic
* The default value for `datacontenttype` is the content type indicated by the serializer

All SDKs will be corrected to deserialize `datacontenttype` from the MQTT PUBLISH `Content Type` field.

All SDKs will continue to use Cloud Event fields for setting only user properties (and no non-user header properties) in PUBLISH messages.

All SDKs will be amended not to set any MQTT header property based on the value of the Cloud Event `datacontenttype` field.

All SDKs will provide a mechanism by which user code can provide a value for the MQTT PUBLISH `Content Type` field.

All SDKs will perform the following validations of Cloud Event fields, which will ensure conformance with the [CloudEvents - Version 1.0.2](https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md) specification:

* `source` is a [URI-reference](https://www.rfc-editor.org/rfc/rfc3986#appendix-A)
* `type` is non-empty
* `specversion` is non-empty
* `dataschema`, if present, is a [URI](https://www.rfc-editor.org/rfc/rfc3986#appendix-A)
* `id` is non-empty
* `subject`, if present, is a non-empty string
* `datacontenttype` conforms to regex `^([-a-z]+)/([-a-z0-9\.\-]+)(?:\+([a-z0-9\.\-]+))?$`, in accordance with the BNF defined in [RFC2045](https://datatracker.ietf.org/doc/html/rfc2045)
* `time`, if present, is a time that will serialize per [RFC3339](https://datatracker.ietf.org/doc/html/rfc3339)

A set of METL test cases will be written to ensure that the above behaviors are implemented consistently across SDKs.

An SDK may delegate checking URI conformance to a standard library for its implementation language.
Any error in a standard library's URI conformance check should not be considered as an error in the SDK, since the standard library's type is what will be used by the customer code that employs the SDK.

## Alternatives Considered

There are no alternatives under consideration for addressing the improper serialization of the CloudEvents `datacontenttype` attribute.

There are no alternatives under consideration for addressing the inconsistencies across SDK implementations.

An alternative flow was considered in which the Cloud Event would set the value of the MQTT PUBLISH `Content Type` field from the Cloud Event `datacontenttype` field.
This approach was rejected because it prevents the Cloud Event from being decoupled from the Telemetry sender.
