# Components of the SDKs

The following outlines the major components of the SDKs and the protocol compiler. This includes clients for connecting to the various services within Azure IoT Operations, as well as additional tooling to support creating edge applications.

The following components groups are included:
* [MQTT](#mqtt)
* [Protocol](#protocol)
* [Services](#services)
* [Protocol compiler (Codegen)](#protocol-compiler-codegen)
* [Akri Services *(Private Preview)*](#akri-services-private-preview)
* [Component limitations](#component-limitations)

## MQTT 

### Session client

This client provides a seemless way to connect an application to the MQTT Broker and other Azure IoT Operations services. It takes care of configuration, connection, reconnection, authentication and security.

## Protocol

Protocol contains Telemetry, Commands and Serialization. Telemetry consists of a sender and a receiver. Command provides an invoker and an executor.

### Telemetry sender

Sends, or publishes, a message to a MQTT topic with a specified serialization format. This supports [CloudEvents](https://cloudevents.io) for describing the contents of the message.

### Telemetry receiver

Receives (via subscription) a telemetry message from a sender. It will automatically deserialize the payload and provide this to the application.

### Command invoker

The invoker is the origin of the call (or the client). It will generate the command along with its associated request payload, serialized with the specified format. The call is routed via the MQTT broker, to the RPC executor. The combination of the invoker, broker and executor are responsible for the lifetime of the message and delivery guarantees. There can be one or more invokers for each executor.

### Command executor

The executor will execute the command and request payload, and send back a response to the invoker. There is typically a single invoker per executor for each command type, however the usage of shared subscriptions can allow for multiple executors to be present, however each invocation will still only be executed one time (the MQTT Broker is responsible for assigning the executor to each command instance).

### Serializers

The serializer pattern allows customer serialization to be used on the MQTT messages. Textual formats such as JSON are a popular payload format, however for large data structure or constrained network links, a binary format may be more desired.

## Services

### State store client

The state store client communicates with the [state store](https://learn.microsoft.com/azure/iot-operations/create-edge-apps/concept-about-state-store-protocol) (a distributed highly available key value store), providing the ability to set, get, delete and observe key/value pairs. This provides applications on the edge a place to securely persist data which can be used later or shared with other applications.

### Schema registry client

The schema registry client provides an interface to get and set Schemas from the Azure IoT Operations [schema registry](https://learn.microsoft.com/azure/iot-operations/connect-to-cloud/concept-schema-registry). The registry would typically contain schemas describing the different assets available to be consumed by the an edge application.

### Leader election client

The leader election client utilized the state store to designate which instance of an application is the leader. Once a single leader is assigned, that instance can then be given special responsibilities that allow all the instances to work together.

### Lease lock client

The lease lock client allows the application to create a lock on a shared resource (a key within the state store), ensuring that no other application can modify that resource while the lock is active. This is a key component of the leader election algorithm.

## Protocol compiler (Codegen)

The [Protocol compiler](/codegen) is a command line tool distributed as a NuGet package. It generates client libraries and server stubs in multiple languages from a [DTDL](https://github.com/Azure/opendigitaltwins-dtdl) input.

The primary purpose of the tool is to facilitate communication between two edge applications via the MQTT broker.

## Akri Services *(Private Preview)*

Akri Services is currently in private preview and not deployed as part of Azure IoT Operations. The following clients will not function unless you are part of the private preview.

### Asset monitor client

The Asset monitor client provides the Akri connector with the AEP *(Asset Endpoint Profile)* and the associate Assets. 
* AEP - Connection information such as the hostname, port, username, password and certificates needed to authenticate with customers on-prem service. 
* Asset - Describes how the asset is accessed within the AEP and a description of the expected payload

### Akri client

Notifies of newly discovered assets, which can then be triaged by the operator.

## Component limitations

### State store

The state store does not support resuming connections. In the case of a disconnect of the client, the client will need to re-observe keys of interest. This has the following implications:

1. Any key notifications that occurred when the client was disconnected are lost, the notifications should not be used for auditing or any other purpose that requires a guarantee of all notifications being delivered.

1. When reconnecting, the application is responsible for reading the state of any observed keys for changes that occurred while disconnected. The client will notify the application that a reconnect has occurred.

### Session client

By default, the session client will resume a session when it connects (both at first connect, and during a reconnect) using `Clean Start = false`. However at first connect, the SDK is unable to have retained the Session State from the past session, which is **not** compliant with [4.1.0-1](https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901231).
 
In this situation, if the application is restarted **after** a `PUBLISH` is sent but **before** the `PUBACK` is received, then this missing Session State may result in lost messages, as the client would be unable to process whether the `PUBLISH` was successful or not and therefor cannot initiate a retry, or report the failure.
