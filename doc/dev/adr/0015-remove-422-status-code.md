# ADR15: Remove 422 Status Code

## Context: 

> NOTE: For simplicity in this ADR, when I refer to a 500 status code, I only mean the scenario where it refers to an Execution Error (when the `__apErr` user property is true), and not other determinations that can come out of a 500 status code (Internal Logic Error or Unknown Error).

We have two HTTP status codes in our error model that indicate an application error from the executor: 422 for "Invocation Error" and 500 for "Execution Error." However, these definitions are merely suggestions for the application writer, and we cannot enforce distinct meanings for these codes. Consequently, within the SDK, there is no real semantic difference between the two scenarios - both signify an application-reported error. So the distinction is only for the executor application to communicate to the invoker application, and we cannot ensure that the executor adheres to our suggested classifications.

While one might argue that this allows customer applications to define multiple error scenarios, in practice, they would often require more than two options and want to provide more detailed information than just an error message. As a real world example, the State Store Service does not return an error status code for error scenarios but instead uses variations of their success status code payload to convey more information about any errors.

## Decision: 

All reference to the 422 status code and Invocation Exception/Error error kind should be removed. Command Executors should no longer send a 422 error. Command Invokers should no longer expect 422 status codes, and this should be treated as an unknown status code, although we should (potentially temporarily) expose the property_name and property_value if present to maintain easy backward compatibility for Schema Registry.
At the API surface, there should no longer be a way for the application to indicate an error vs a success response, they should only have a way to respond (that encompasses both, but is internal to their encoding). The SDK should only send a 500 if there is an uncaught exception thrown from the application (or the language equivalent that indicates the application will be unable to respond).

This change is a wire protocol change, but we will not increase the version since we are before public preview, and the only known usage of the 422 status code is with Schema Registry, which we can manage during the transition since we own the Client side code that would be affected by this change.

## Alternatives Considered:

1. Allowing the application to send more error codes. Previous conversations have deemed it innappropriate for the user to be able to set the status code directly, and if we provided a set of error kinds available to them, what would determine the number of options we provide?
1. Maintaining 422 as is. Arguments against this include previous (although unformalized until this document) buy in for this change across languages and Rust currently being implemented as this ADR proposes, so code changes would still be needed.
1. Allow custom_user_data to be sent on application error responses. This would likely be in combination with still removing the 422 status code.
1. Allow custom_user_data and a response_payload on error responses to provide the most flexibility for the customer to be able to signal that a response is an error, while providing any additional information that they'd like.

## Consequences:

1. Changes needed across languages to support this new functionality.
    - Remove option for executor application to specify the error type (For Rust, this was previously sent as a 500, but should have been sent as a 422)
    - Remove handling of the 422 status code in the invoker.
    - Include property_name and property_value for unknown status codes if they are present.
    - Remove 422 as a known Status Code.
    - Remove InvocationException as an AIO Protocol Error kind.
1. METL tests to update: `CommandExecutorUserCodeRaisesContentError_RespondsError`, `CommandInvokerResponseIndicatesInvocationError_ThrowsException`, and `CommandExecutorUserCodeRaisesContentErrorWithDetails_RespondsError`. Double check that the equivalent 500 scenario is already captured in METL before removing these.
1. Documentation changes needed (for 422 and InvocationException)
    - https://github.com/Azure/iot-operations-sdks/blob/main/doc/reference/error-model.md#appendix-1-error-conditions-in-the-c-sdk
    - https://github.com/Azure/iot-operations-sdks/blob/main/doc/reference/command-errors.md


## Open Questions:

1. ~~Do we want to consider any of the alternatives that involve allowing returning more information on error responses? These would definitely come with larger code changes than the current proposal.~~ This change handles this in a different way
1. ~~422 currently allows the user to specify an invalid property name and an invalid property value - do we want to maintain this optionally for 500 errors?~~ No, because 500 errors should not be sent intentionally from the application.

