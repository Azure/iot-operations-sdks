# ADR11: Cloud Event API Relationship

## Status: 

PROPOSED

## Context: 

Cloud Events should be added for most/all MQ Telemetry Messages, and they must be optional. They must use the public format within user properties, which does not start with our reserved prefix. They are something we currently must easily support, but including cloud events on the Telemetry Message object opens the door for breaking API changes if we want to add easy functionality for other similar concepts in the future.

## Decision: 

The proposal is to have Cloud Events be built with convenience functions into `custom_user_data` and not a part of the `telemetry_message` API. This provides flexibility for us to add more similar things in the future without breaking changes, as well as the flexibility to change/deprioritize cloud events in the future if ever needed.



## Alternatives Considered:

1. No API change, but providing more documentation around when/how to use Cloud Events. This should still occur.

## Consequences:
API functions needed:
- Cloud Event Builder in language idiomatic way that provides default values for specified fields and validations on all fields as specified in [0010-cloud-event-content-type.md](./0010-cloud-event-content-type.md). Note that this will require knowledge of the Telemetry Sender for default values (such as the Telemetry Topic for the `subject` field).
- to headers function - takes a CloudEvent object and returns an array formatted as User Properties  that can be added to `custom_user_data` that is passed in on the Telemetry Message.
- from headers function - Telemetry Receivers will return cloud event data raw as part of `custom_user_data` and the `content_type` field. This function will take in these User Properties and the MQTT content_type and returns a CloudEvent object.

Note: These API functions are likely present in all languages already, and just need to be made public/fully functioned
