# Components

The following is the major components of the SDKs which are currently available in three main packages.

## MQTT 

1. **Session client** 
   
   This client provides a seemless way to connect an application to the MQTT Broker and other connected service available in Azure IoT Operations. It takes care of configuration, (re)connection, authentication and security.

## Protocol

* **Telemetry sender**

    Sends, or publishes, a message to a MQTT topic with a specified serialization format. This supports [CloudEvents](https://cloudevents.io) for describing the contents of the message.

* **Telemetry receiver**

    Receives (via subscription) a telemetry message from a sender. It will automatically deserialize the payload and provide this to the application.


* **RPC invoker**

    The invoker is the origin of the call (or the client). It will generate the command along with its associated request payload, serialized with the specified format. The call is routed via the MQTT broker, to the RPC executor. The combination of the invoker, broker and executor are responsible for the lifetime of the message and delivery guarantees. There can be one or more invokers for each executor.

* **RPC executor**

    The executor will execute the command and request payload, and send back a response to the invoker. There is typically a single invoker for executor for each command type, however the usage of share subscriptions can allow for multiple executors to be present, however each invocation will still only be executed one time (the MQTT Broker is responsible for assigning the executor to each command instance).

* **Serializers**

    The serializer pattern allows customer serialization to be used on the MQTT messages. Textual formats such as JSON are a popular payload format, however for large data structure or constrained network links, a binary format may be more desired.

## Services

1. **State store client**

    The state store client communicates with the [state store](https://learn.microsoft.com/azure/iot-operations/create-edge-apps/concept-about-state-store-protocol)(a distributed highly available key value store), providing the ability to set, get, delete and observe key/value pairs. This provides applications on the edge a place to securely persist data which can be used later or shared with other applications.

1. **Schema registry client**

    The schema registry client provides an interface to get and set Schemas from the Azure IoT Operations [schema registry](https://learn.microsoft.com/en-us/azure/iot-operations/connect-to-cloud/concept-schema-registry). The registry would typically contain schemas describing the different assets available to be consumed by the an edge application.

1. **ADR client**

    The ADR *(Azure Device Registry)* client provides the application the AEP *(Asset Endpoint Profile)*. The configuration will contain information such as the hostname, port, username, password and certificates needed to connect to customers edge service.

1. **Leader election client**

    The leader election client utilized the state store to designate which instance of an application is the leader. Once a single leader is assigned, that instance can then be given special responsibilities that allow all the instances to work together.

1. **Lease lock client**

    The lease lock client allows the application to create a lock on a shared resource (a key within the state store), ensuring that no other application can modify that resource while the lock is active. This is a key component of the leader election algorithm.

