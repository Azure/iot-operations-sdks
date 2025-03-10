# ADR12: Dynamic Content Type

## Context: 

The Media Broker needs a way to specify different `content_type`s per telemetry message while using a raw data format. For future proofing, we'd like to make `content_type`s specifiable per any message with or without serialization.

## Decision: 

Instead of content type being a static method on the Serializer, the serialize() function should return the serialized payload in bytes as well as the `content_type` and `format_indicator` of the serialized payload. This allows the application to either always return the same `content_type` and `format_indicator` (maintaining existing functionality and convenience), or they can provide information on their generic data type that is passed to the serializer to determine what the `content_type` and `format_indicator` should be.

On the receiving side, the deserialize() function will be provided with the `content_type` and `format_indicator` to aid in deserialization. The SDK will no longer validate that the `content_type` and `format_indicator` are valid for the envoy, so the deserialize() function will now have to return an error if the `content_type` or `format_indicator` is not acceptable.

The `content_type` and `format_indicator` should now be included on the Telemetry Message/Command Request/Response returned to the application so that they have knowledge of the information.

The data type `T` is still 1:1 with an envoy, so this does not affect the desire for one command per invoker/one telemetry per sender. This change only allows flexibility in serialization formats for the same data.

## Alternatives Considered:

1. Allowing Cloud Events to override the content type. With the split in [0011-cloud-events-api.md](./0011-cloud-events-api.md), it isn't desirable for the Protocol to make decisions based on Cloud Event data. This also removes this functionality for anyone not using Cloud Events, which doesn't seem like a necessary restriction.
1. Allow overriding the content type on the Telemetry Message object. The solution proposed in this ADR is preferred to keep all content type settings tied to one place, the serializer.

## Consequences:

1. Changes needed across languages to support this new functionality.

## Open Questions:

1. ~~Should the format indicator have similar flexibility?~~ Yes, captured above
1. ~~Should we provide a simple implementation for payloads that don't get serialized/deserialized in the SDKs? (I think this could be nice)~~ Yes, this will be valuable to showcase how this should be implemented and make the raw case easy.
1. ~~Does the `content_type` need to be returned on the Telemetry Message/Command Request/Response object, or should the implementation be responsible for parsing the information into their generic data type if they want the information (this would be more symmetrical with the sending side, and reduces additional data if the application doesn't need it)~~ Decision: Yes, always return because the information may be valuable.

