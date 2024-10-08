# Protocol Versioning

## The problem

Users may add/upgrade RPC invokers/executors and telemetry senders/receivers over time. In addition, users may use a mix of languages to create these RPC/telemetry components. This mix of client libraries and client library versions could lead to compatibility problems if, for example, the .NET command executor expects one format of RPC request but the Rust command invoker sends requests in a different format.

We need a way to assert compatibility between RPC invokers + executors and telemetry senders + receivers when each component isn't necessarily using the same client library or the same client library version.

Typically, this problem is solved by protocol versioning.

## Definition

"Protocol versioning" is the version assigned to the expected over-the-wire interactions between a client and service. 

For instance, a particular protocol version of our RPC + telemetry stacks would dictate that an RPC request's MQTT publish should include correlation data that is parsable as a GUID.

Note that protocol versioning is distinct from the version of the library that implements this over-the-wire protocol. A given library can make changes that have no impact on what bits are sent over the wire. For instance, a library can upgrade a logging framework dependency and release with that newer dependency version.

## State of the art

There are several sources of inspiration we can take for protocol versioning.

MQTT as a protocol mostly uses the pattern of `Major.Minor` versioning (v3.1, v3.1.1*, v5.0). Matching this `Major.Minor` pattern should allow us to absorb any version bumps that MQTT itself may have beyond the current protocol version (v5.0).

HTTP protocol versioning is also relevant since our RPC pattern itself mimics the basic HTTP request/response pattern. Much like MQTT, HTTP uses the `Major.Minor` protocol versioning strategy.

Finally, gRPC is another relevant source of inspiration since it closely matches our desired RPC protocol. Notably, though, gRPC does not use protocol versioning as it does not allow for the over-the-wire behavior to change even if the client library bumps its major version.

> Most importantly, a [gRPC client library] major version increase must not break wire compatibility with other gRPC implementations so that existing gRPC libraries remain fully interoperable

See [here](https://github.com/grpc/grpc/blob/master/doc/versioning.md#versioning-overview) for more details.

Given that we may want to evolve the RPC/Telemetry protocol over time, this approach seems unsuitable.

*MQTT protocol version 3.1.1 included changes to the protocol that could be considered worth a minor version bump (allowing ClientIds to be much longer, for instance). It is unclear why this version was not just v3.2

## The proposal

Each MQTT publish sent as part of an RPC request/response or telemetry message will include a protocol version user property. That protocol version will be composed of only a major and a minor version. These two version numbers will follow standard semantic versioning definitions.

A protocol version minor bump signifies a backwards and forwards compatible change. An example change that a minor version bump would cover is if the command executor now allows an **optional** and **ignorable**/**defaultable** new user property on each command request.

A protocol version major bump signifies a non-backwards or non-forwards compatible change. An example non-backwards compatible change is if the command executor now **requires** a new user property on each request.

The required MQTT user property will use the name "__protVer" and the value will be "X.Y" whereX is the major protocol version and Y is the minor protocol version. The first value of this property will be "1.0", for example.

With the above, a command invoker with protocol version X.Y can interact with a command executor with protocol version X.Z for any value of Y and Z. 

The protocol version used by a client will be hardcoded in the client library and cannot be changed by the user. Users will need to upgrade/downgrade their client library version to change the protocol version. 

Since command invokers and executors both send publishes and receive publishes, a single protocol version will cover all publishes sent/received by command invokers and executors. To put it differently, there is no separate protocol version for just the expected on-the-wire behavior of a command response. 

### What if I use mis-matched protocol versions?

#### RPC

If a command invoker sends a request with a protocol version that the command executor
cannot handle, the command executor should send a command response that includes the standard headers (including "__protVer") as well as:

* A status of "Version Not Supported" (505)
* A user property with name "__supProtMajVer" and a value of a space-separated list of major protocol versions that the command executor does support
* A user property with name "__requestProtVer" and a value of the protocol version that the rejected request had.

Once that response is received by the command invoker, the invoker should abort the request and report this "Unsupported Request Version" error back to the user by whatever convention the language typically uses. See [this document](./error-model.md) for details on how each language should report an error. The error reported to the user should specify an error kind of "unsupported request version". The error reported by the invoker should also include both the protocol version the request had as well as the protocol versions that the command executor supports. This allows for application level logic to handle negotiating protocol versions if necessary.

----------------

If a command invoker receives a response with a protocol version that it does not support, the invoker should abort the request and report the error to the user similar to the above case except specify an error kind of "unsupported response version". This error should include both the protocol version that the response specified as well as the major protocol versions that this command invoker supports.

#### Telemetry

Unlike with RPC, there is no feedback to the user if they try to publish telemetry and no active telemetry receiver supports the request's protocol version. See [this section](#outstanding-questions) for more details on this problem.

## Example upgrade scenarios

The below sections discuss how a typical user would go about deploying/redeploying their command invokers/executors and telemetry senders/receivers when trying to upgrade them to a newer protocol version. Note that users don't need to follow the below instructions if they are upgrading to a client library version that doesn't change the protocol version. 

### Minor protocol version upgrade scenarios

The below scenarios cover recommended upgrade scenarios with various sets of command invokers/executors and telemetry senders/receivers when upgrading one or more minor protocol versions.

#### Single invoker, single executor

Either the invoker or the executor can be upgraded first without disrupting the other since minor version bumps are both backwards and forwards compatible.

#### Multiple invokers, multiple executors

Any of the invokers or the executors can be upgraded in any order without disrupting the other since minor version bumps are both backwards and forwards compatible.

#### Single telemetry sender, single telemetry receiver

Either the sender or the receiver can be upgraded first without disrupting the other since minor version bumps are both backwards and forwards compatible.

#### Multiple telemetry senders, multiple telemetry receivers

Any of the senders or the receivers can be upgraded in any order without disrupting the other since minor version bumps are both backwards and forwards compatible.

### Major protocol version upgrade scenarios

The below scenarios cover recommended upgrade scenarios with various sets of command invokers/executors and telemetry senders/receivers when upgrading one or more major protocol versions.

#### Single invoker, single executor

To manage an upgrade like this, the user will need to have application layer logic to retry RPC requests with a different command invoker depending on if the initial request failed due to an "unsupported request version" error.

With that in place already, a user will first need to create a parallel invoker with the bumped protocol major version. Then the user can upgrade the command executor in place. During this transition, the old invoker may make a request to the newly upgraded executor that gets rejected. That request should then be re-sent by the newer invoker. At this moment, the user can remove the old invoker.

#### Multiple invokers, multiple executors

Similar to the single invoker/single executor case. The user must start by adding the new parallel invoker and then upgrade one of the executors in place. Beyond that point, the user can upgrade any of the remaining invokers/executors in place in any order.

#### Single telemetry sender, single telemetry receiver

The user cannot rely on the same pattern as the single invoker/single executor case since a telemetry receiver cannot send an "unsupported request version" error back to the telemetry sender.

Instead, the user will need to create an upgraded telemetry receiver in parallel with the existing telemetry receiver. Then the user can upgrade the telemetry sender in place. Finally, they can delete the old telemetry receiver.

#### Multiple telemetry senders, multiple telemetry receivers

Same pattern as the single sender/single receiver case. A user must start by adding an upgraded receiver in parallel and then upgrade a single sender in place. Beyond that point, the user can upgrade the remaining senders/receivers in place in any order.

## Outstanding questions

Do we care about exposing protocol versions of RPC services that Microsoft defines like schema registry? If so, how do we expose the protocol versions they use so that users can create clients?

How does a telemetry sender know if the telemetry it sent was accepted by any telemetry receivers? If a telemetry sender sends a request with protocol version 1.5 but all the receivers use protocol version 1.4, then, as a user, I would expect some sort of "Unsupported API version" error, but we don't currently have a mechanism for this. Malformed telemetry requests (such as when a telemetry receiver receives a message with an unexpected content type) are currently acknowledged but otherwise ignored. This problem isn't specific to protocol versioning, but it is relevant.

## Appendix

### Separate Protocol Versions For Telemetry vs RPC?

The interactions between telemetry senders/receivers and command invokers/executors are disjoint, so we could consider maintaining separate protocol versions for the 
separate interactions between them. That would allow for us to make changes to the RPC protocol without affecting the telemetry protocol. However, this would also make our planned mapping of client library compatibility much more complicated, so I propose we only maintain one protocol version for all of telemetry + RPC interactions.

### Tracking protocol versions across languages + library versions

Our team will need to maintain a table that specifies which versions of each of the .NET/Go/Rust client libraries use which protocol versions. The table would need to be updated upon each release of a new client library version. Potentially, it could look something like:

| Protocol Version | .NET package versions | Go package versions | Rust package versions |
| --- | --- | --- | --- |
| 1.0 | 1.0.0, 1.01, 1.02 | 1.0.0 | 1.0.0 |
| 1.1 | 1.1.0, 2.1.0 | 1.1.0 | 1.1.0, 1.1.1, 1.2.0 | 
| 2.0 | 2.0.0, 2.1.0 | 2.0.0, 3.0.0 | 2.0.0 | 

A table like this would make it easy for a user to see what versions of our client libraries are compatable with one another.
