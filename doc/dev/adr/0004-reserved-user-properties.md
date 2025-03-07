# ADR4: Reserved User Properties and the Fencing Token

## Context: 

The Fencing Token user property is exposed as a standard optional field on Command Requests, although the only current use of it is with the State Store Service Client. We have received questions from consumers of the SDK about when they should or should not be using this field, and what we are doing with the value, and the answer seems to be that they shouldn't use it outside of State Store scenarios, and that we don't do anything other than attach it as a user property. Currently, the fencing token field has to be exposed this way because it starts with our Protocol's reserved prefix `__`, which is validated against, so the State Store Service Client could not add this user property itself.

## Decision: 

The proposal is to change our strict validation of preventing the user application from using the reserved prefix (`__`) in it's provided user properties (custom_user_data) to having this only be a convention. This convention would detail that the reserved prefix `__` should only be used by Azure IoT Operations SDK's MQTT, Protocol, and Services packages/crates (and any other packages/crates shipped as a part of this repo in the future), and any use of the reserved prefix by consumers outside of these packages could cause unexpected behavior now or in the future. Note that this includes other Microsoft consumers of this SDK, as there would be no central place to ensure uniqueness of user property names.

## Alternatives Considered:

1. Changing the State Store Service to use the user property name of `ft` instead of `__ft` was considered to maintain the reservation of this prefix. This change would have a larger impact than the one proposed, and doesn't solve the issue if something like this comes up again for a different service.

1. Remove the reserved prefix of "__" being validated against in the SDKs, and only reserve specific user property names instead. This decision wouldn't be extensible if we want to reserve other property names in the future.

## Consequences:

- Fencing Token will no longer be listed in [Command Metadata](https://github.com/Azure/iot-operations-sdks/blob/main/doc/reference/message-metadata.md#command-metadata), and all languages will need to make this API change.
- If it is not already, convenience functions should be provided in all languages to convert a Hybrid Logical Clock to and from the format that is used for the user property value.
- Documentation around the reserved prefix will need to be updated in code and in general docs.
- No Protocol Version update should be needed, as messages will stay the same over the wire.
- State Store Service Clients will need to be updated to add the fencing token field to custom_user_data instead of passing it in on the Command Request (no user facing API change needed).

## Open Questions:

- Additional conversations around the `__ts` field and cloud events are still in progress and could affect/be affected by this decision.

