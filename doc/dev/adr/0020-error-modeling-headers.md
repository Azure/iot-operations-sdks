# ADR 19: Modeling User Errors

## Context

We have received feedback from users that they would like to have RPC responses communicate application level failures in the headers of the response rather than the payload of the response. 

This feedback is, in part, because some applications want to route RPC responses without reading/deserializing the entire payload. It is also partly because [some applications cannot extend or change the payload model to acommodate error handling.](https://github.com/Azure/iot-operations-sdks/issues/488#issuecomment-2707496996).

## Decision

Our SDKs will add APIs on the command executor side that mark an RPC response as an "application error" by attaching two new MQTT user properties ("AppErrCode" and "AppErrPayload") whose values are a user-defined string and a user-defined payload object (serialized to bytes and then encoded as a UTF8 string) respectively. When a user wants to indicate that an RPC call failed with an application error they must provide the "AppErrCode" value, but "AppErrPayload" is optional.

In addition, users must be able to include arbitrary user properties in their command response to support this error reporting in case our standard fields are insufficient. This feature is likely in place already for all languages.

On the command invoker side, we will add APIs for checking if a response was an application error and returning the error code and error data fields parsed from the MQTT message "AppErrCode" and "AppErrPayload" user properties.

Similar to how our SDKs handle serializing the actual MQTT message payload, our SDKs will require the user provide the serializer for serializing/deserializing the AppErrPayload object.

Other than these two new user properties, the over-the-wire behavior of our protocol won't change as a result of this decision.

In order to provide a strongly-typed experience, we will also add codegen support for modeling both the possible error codes (enum-like list of possible values?) and the type of the error payload in DTDL. This modeling will be detailed in a separate ADR, though.

## Code Example



### Enforcement

Note that, while this decision creates a standardized way of communicating application errors, our SDK will **not** enforce that users communicate application errors with this standard. Users will still be able to model application errors in response payloads or in custom user properties if they prefer. There is no way to force users to communicate errors with this standard pattern.

## Samples

We want to establish this pattern as the standard for communicating RPC application errors. To do so, we must either update or add a sample per-language that demonstrates handling at least one application error.

For instance, we would make the counter service return an application error if the invocation specifies a negative value to increment by. This change won't include any DTDL level changes yet.

## Open Questions

- MQTT user properties are strings, not bytes, so is there an issue with using UTF8 encoding to convert a serialized payload to a string? Does this work for the serializers we already support (protobuf, avro, raw, etc.)
  - Maybe this suggests the serialization interface we use here converts object <-> string rather than object <-> bytes

- MQTT user property value size limit considerations? Users may provide very large payload objects and MQTT as a protocol may not be built for that?

- Do we remove support for modeling application errors in the MQTT message payload as described in (ADR 19)[./0019-codegen-user-errs.md]? No one has onboarded to this model yet

- Do we need distinct serializers for MQTT message payload serialization and this new MQTT user property "payload" serialization? Is it possible that, for example, errors would be modeled as JSON objects but normal payloads are modeled as protobuf objects? 