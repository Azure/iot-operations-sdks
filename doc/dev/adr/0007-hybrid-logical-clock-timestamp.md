# ADR7: Hybrid Logical Clock Use and Timestamp User Property

## Status: 

PROPOSED

## Context: 

Rust has an "implementation" of the HLC (Hybrid Logical Clock) in that it supports the data type (timestamp, counter, and node_id) and creating a new HLC from SystemTime::now(). The other languages also have additional functionality such as Update, CompareTo, Validate, etc. To reach feature completeness in Rust, I started investigating what this additional functionality should be used for in Rust, and to make sure it was needed (since nothing seemed to be broken yet without it). What I learned:
- The goal of having these additional functions is to update a local global HLC whenever a message is received (any of command requests/responses or telemetry).
- The global HLC is maintained by us and is internal (although I could see a desire for the customer to do something similar in their application with the timestamps they receive and use our HLC functions to do so).
- The global HLC we maintain is used for the timestamp that is sent on telemetry and command messages.
- A current issue with this implementation is that when we receive a message from the State Store Service, the `__ts` user property doesn't actually refer to the current time that the message was sent (as we expect), but rather the version of the key
- Some notes on current implementations (although these are gaps in the implementation rather than intentional):
  - Currently in dotnet, the executor updates the global HLC based on incoming messages (in conjunction with the local clock), and it is not updated again before the response is sent. The invoker updates the global HLC against the local clock before a request is sent, but it is not updated based on received responses. The telemetry sender updates the global HLC against the local clock before sending messages. And the telemetry receiver does not update the global HLC.
  - In go, all publishes update the global HLC with the local clock before attaching the timestamp to the message. Right now, it doesn't get updated from any inbound messages.


## Decision: 

1. A (reference to a) global HLC should be created by the application and passed into all envoy new functions. This allows the application to adjust the max clock drift, as well as have read access to the global HLC. The SDK will update this value and use it on outbound messages. There is no restriction on all HLCs passed in to all envoys matching, but it is encouraged to maintain predictable behavior.
1. All incoming messages with a `__ts` timestamp property will update the global HLC.
1. All outbound messages will update the global HLC against the system clock and then use that value for the `__ts` property.
1. The app MUST have read access to the global HLC so they can use it for ordering, creating fencing tokens, etc.
1. `__ts` will be maintained as a user property that has meaning in the SDKs.
1. We will push to have the State Store Service send the message timestamp on the `__ts` user property to match our convention and send the version under a different user property name. If this isn't possible, updating our global HLC with a timestamp that is far in the past will not cause any errors on Update, but it won't be doing anything to make our global HLC more accurate.

## Alternatives Considered:

1. Have the application maintain full ownership of the global HLC - they could update it on any timestamps received (and not update it on State Store timestamps received), and create timestamps from it for outgoing messages. Cons: Adds a lot of overhead for applications for something that most would want us to provide the functionality of updates and creating timestamps for messages. Additionally, this change would be motivated by one service handling the `__ts` value incorrectly, and requiring the application to do more of this work would likely introduce more errors for the value than reduce them.
1. Don't update the global HLC on received command responses and telemetry messages. The current dotnet and Go implementations already do this, but from our conversation this was a gap in the implementation rather than a desired feature.
1. We could add an additional user property that is an optional boolean determining whether we should use the timestamp to update the local HLC or not. This would resolve the issue from the State Store Service as it would not include this new user property, so we wouldn't update the global HLC when we receive messages from the State Store. Since updating the global HLC from state store messages that may have older `__ts` values won't make the global HLC less accurate though, adding this work-around felt like it added minimal value for additional complication.


## Consequences:
- Code updates will need to be made across all three languages to get up to date with these spec decisions, since all are missing some/all functionality.
- Current implementations don't validate that incoming messages' timestamps aren't too skewed from the local HLC, although this was accidental. Updating the implementations to match our expectations will cause this as a possible failure point, which is why we are allowing the application to set the max acceptable clock drift.
- Bug bashing will need to be done to make sure there aren't unintended consequences of fully implementing this feature.

## Open Questions:
- Is it possible for the State Store Service to send the `__ts` user property with a value that matches our semantic expectations?
