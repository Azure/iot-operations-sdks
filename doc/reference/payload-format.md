# Payload Format

The data in Telemetry and Command payloads can be serialized in various formats.
When an Envoy is instantiated, an appropriate serializer is also instantiated based on a payload format designator.

One option for data format designators is [MIME type](https://developer.mozilla.org/en-US/docs/Web/HTTP/Basics_of_HTTP/MIME_types) such as "application/json" and "application/avro".
However, despite the fact that MIME types are widely used, they do not fully specify the version of the data format.

A related designation mechanism is used by [xRegistry](https://github.com/xregistry/spec/blob/main/schema/spec.md#schema-formats) to define a set of identifiers for schema formats.
These have the advantage of fully specifying the format version.
However, the relationship between schema format and data format is not always one-to-one.
Some standard data formats, such as [CBOR](https://cbor.io/), have no standard schema format.
Some standard schema formats, such as [JSON Schema](https://json-schema.org/), can define schemas that apply to more than one data format, e.g., [JSON](https://www.json.org/json-en.html) and [CBOR](https://cbor.io/).

The payload format designators defined herein follow the [xRegistry naming convention](https://github.com/xregistry/spec/blob/main/schema/spec.md#format) of `{NAME}/{VERSION}`.
Furthermore, for those cases in which there is a one-to-one correspondence between schema format and data format, the xRegistry schema format identifier is used as a payload format designator.
For those cases without such a one-to-one relationship, payload format designators are chosen to adhere to the xRegistry standard, using the standardization number as a version identifier when no other version numbering is available.

## Payload format designators

Following is the set of payload format designators recognized by the Codegen toolchain.

| Payload format designator | Data serialization format |
| --- | --- |
| `Avro/1.11.0` | [Apache AVRO](https://avro.apache.org/docs/) data serialization format |
| `Cbor/rfc/8949` | RFC 8949 Concise Binary Object Representation ([CBOR](https://cbor.io/)) data format |
| `Json/ecma/404` | ECMA-404 JavaScript Object Notation ([JSON](https://www.json.org/json-en.html)) data interchange syntax |
| `Protobuf/2` | Google [Protocol Buffers](https://protobuf.dev/) data interchange format, [version 2](https://protobuf.dev/programming-guides/proto2/) |
| `Protobuf/3` | Google [Protocol Buffers](https://protobuf.dev/) data interchange format, [version 3](https://protobuf.dev/programming-guides/proto3/) |
| `raw/0` | unserialized raw bytes |
